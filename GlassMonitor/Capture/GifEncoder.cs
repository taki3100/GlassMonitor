using System.IO;

namespace GlassMonitor.Capture;

/// <summary>
/// ループ再生するアニメーション GIF を書き出す（--record 用）。
/// <list type="bullet">
/// <item>パレットは最初のフレームから作った 255 色を全フレームで使う（UI の色はほとんど変わらないため）。</item>
/// <item>前のフレームから変わらないピクセルは透明にし、変わった範囲だけを書き込む（ファイルを小さくするため）。</item>
/// <item>ディザリングはしない（フレームごとにノイズが変わって、かえって大きくなるため）。</item>
/// </list>
/// </summary>
internal sealed class GifEncoder(Stream stream, int width, int height)
{
    private const int MaxColors = 255;
    private const byte TransparentIndex = 255;
    private const int MinCodeSize = 8;

    private readonly Lzw _lzw = new();
    private Palette? _palette;
    private byte[]? _shown;
    private (byte[] Indices, TimeSpan Time)? _pending;

    /// <summary>フレームを追加する。表示時間は次のフレームとの時刻の差になる。</summary>
    /// <param name="bgra">不透明の Bgra32（または Pbgra32）のピクセル。</param>
    /// <param name="time">録画を始めてからの時刻。</param>
    public void AddFrame(byte[] bgra, TimeSpan time)
    {
        if (_palette is null)
        {
            _palette = Palette.Create(bgra, MaxColors);
            WriteHeader(_palette);
        }

        var indices = _palette.Map(bgra);
        if (_pending is { } pending)
            WriteFrame(pending.Indices, time - pending.Time);
        _pending = (indices, time);
    }

    /// <summary>最後のフレームを書いて GIF を閉じる。</summary>
    /// <param name="end">最後のフレームを表示し終える時刻。</param>
    public void Finish(TimeSpan end)
    {
        if (_pending is { } pending)
            WriteFrame(pending.Indices, end - pending.Time);
        stream.WriteByte(0x3B);
    }

    private void WriteHeader(Palette palette)
    {
        Write("GIF89a"u8);
        WriteUInt16(width);
        WriteUInt16(height);
        stream.WriteByte(0xF7); // 256 色のグローバルカラーテーブルあり、色深度 8 ビット
        stream.WriteByte(0);    // 背景色の番号
        stream.WriteByte(0);    // ピクセルの縦横比（指定なし）
        for (int i = 0; i < 256; i++)
        {
            var (r, g, b) = i < palette.Colors.Length ? palette.Colors[i] : ((byte)0, (byte)0, (byte)0);
            stream.WriteByte(r);
            stream.WriteByte(g);
            stream.WriteByte(b);
        }

        // NETSCAPE2.0 拡張: 無限にループ再生する
        Write([0x21, 0xFF, 0x0B]);
        Write("NETSCAPE2.0"u8);
        Write([0x03, 0x01, 0x00, 0x00, 0x00]);
    }

    private void WriteFrame(byte[] indices, TimeSpan duration)
    {
        // 前のフレームから変わった範囲（最初のフレームは全体）
        var (left, top, right, bottom) = _shown is null ? (0, 0, width - 1, height - 1) : ChangedBounds(_shown, indices);
        bool empty = right < left;
        if (empty)
            (left, top, right, bottom) = (0, 0, 0, 0);

        int frameWidth = right - left + 1;
        int frameHeight = bottom - top + 1;
        var pixels = new byte[frameWidth * frameHeight];
        for (int y = 0; y < frameHeight; y++)
        {
            for (int x = 0; x < frameWidth; x++)
            {
                int i = (top + y) * width + left + x;
                pixels[y * frameWidth + x] = _shown is not null && _shown[i] == indices[i] ? TransparentIndex : indices[i];
            }
        }

        // グラフィック制御拡張: 表示時間（1/100 秒）、前のフレームを残したまま重ねる、透明色あり
        int delay = Math.Max(2, (int)Math.Round(duration.TotalMilliseconds / 10));
        Write([0x21, 0xF9, 0x04, (1 << 2) | 0x01]);
        WriteUInt16(delay);
        Write([TransparentIndex, 0x00]);

        // イメージ記述子（ローカルカラーテーブルなし）
        stream.WriteByte(0x2C);
        WriteUInt16(left);
        WriteUInt16(top);
        WriteUInt16(frameWidth);
        WriteUInt16(frameHeight);
        stream.WriteByte(0);
        _lzw.Write(stream, pixels, MinCodeSize);

        _shown = indices;
    }

    private (int Left, int Top, int Right, int Bottom) ChangedBounds(byte[] before, byte[] after)
    {
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (before[row + x] == after[row + x])
                    continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = y;
            }
        }
        return (left, top, right, bottom);
    }

    private void Write(ReadOnlySpan<byte> bytes) => stream.Write(bytes);

    private void WriteUInt16(int value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
    }

    /// <summary>メディアンカットで作ったパレット。色は RGB 各 6 ビットに丸めて扱う。</summary>
    private sealed class Palette
    {
        private const int Bits = 6;
        private const int Levels = 1 << Bits;

        // 丸めた色ごとの、最も近いパレットの番号（未計算は -1）
        private readonly short[] _nearest = new short[Levels * Levels * Levels];

        private Palette((byte R, byte G, byte B)[] colors)
        {
            Colors = colors;
            Array.Fill(_nearest, (short)-1);
        }

        public (byte R, byte G, byte B)[] Colors { get; }

        public static Palette Create(byte[] bgra, int maxColors)
        {
            var counts = new int[Levels * Levels * Levels];
            for (int i = 0; i < bgra.Length; i += 4)
                counts[Key(bgra[i + 2], bgra[i + 1], bgra[i])]++;

            var used = Enumerable.Range(0, counts.Length).Where(k => counts[k] > 0).ToArray();
            var boxes = new List<int[]> { used };
            while (boxes.Count < maxColors)
            {
                // ピクセル数 × 色の幅が最も大きい箱を、幅の最も広い成分の中央（ピクセル数で重み付け）で分ける
                int[]? largest = null;
                double best = 0;
                foreach (var box in boxes.Where(b => b.Length > 1))
                {
                    double score = box.Sum(k => (double)counts[k]) * Spread(box).Range;
                    if (score > best)
                        (best, largest) = (score, box);
                }
                if (largest is null)
                    break;

                int channel = Spread(largest).Channel;
                var sorted = largest.OrderBy(k => Component(k, channel)).ToArray();
                long half = sorted.Sum(k => (long)counts[k]) / 2;
                long sum = 0;
                int split = 1;
                for (; split < sorted.Length - 1; split++)
                {
                    sum += counts[sorted[split - 1]];
                    if (sum >= half)
                        break;
                }
                boxes.Remove(largest);
                boxes.Add(sorted[..split]);
                boxes.Add(sorted[split..]);
            }

            // 箱ごとに、ピクセル数で重み付けした平均の色にする
            var colors = boxes.Select(box =>
            {
                double total = box.Sum(k => (double)counts[k]);
                return (
                    ToByte(box.Sum(k => counts[k] * Expand(Component(k, 0))) / total),
                    ToByte(box.Sum(k => counts[k] * Expand(Component(k, 1))) / total),
                    ToByte(box.Sum(k => counts[k] * Expand(Component(k, 2))) / total));
            }).ToArray();
            return new Palette(colors);
        }

        /// <summary>Bgra32 のピクセルをパレットの番号に置き換える。</summary>
        public byte[] Map(byte[] bgra)
        {
            var indices = new byte[bgra.Length / 4];
            for (int i = 0; i < indices.Length; i++)
            {
                int key = Key(bgra[i * 4 + 2], bgra[i * 4 + 1], bgra[i * 4]);
                short index = _nearest[key];
                if (index < 0)
                    _nearest[key] = index = Nearest(key);
                indices[i] = (byte)index;
            }
            return indices;
        }

        private short Nearest(int key)
        {
            int r = Expand(Component(key, 0)), g = Expand(Component(key, 1)), b = Expand(Component(key, 2));
            short best = 0;
            int bestDistance = int.MaxValue;
            for (short i = 0; i < Colors.Length; i++)
            {
                var (cr, cg, cb) = Colors[i];
                int distance = (cr - r) * (cr - r) + (cg - g) * (cg - g) + (cb - b) * (cb - b);
                if (distance < bestDistance)
                    (bestDistance, best) = (distance, i);
            }
            return best;
        }

        private static (int Channel, int Range) Spread(int[] box)
        {
            var ranges = Enumerable.Range(0, 3)
                .Select(c => box.Max(k => Component(k, c)) - box.Min(k => Component(k, c)))
                .ToArray();
            int channel = Array.IndexOf(ranges, ranges.Max());
            return (channel, ranges[channel]);
        }

        private static int Key(byte r, byte g, byte b) =>
            ((r >> (8 - Bits)) << (Bits * 2)) | ((g >> (8 - Bits)) << Bits) | (b >> (8 - Bits));

        /// <summary>0 = R、1 = G、2 = B（6 ビット）。</summary>
        private static int Component(int key, int channel) => (key >> (Bits * (2 - channel))) & (Levels - 1);

        /// <summary>6 ビットの値を 8 ビットの範囲の中央に戻す。</summary>
        private static int Expand(int value) => (value << (8 - Bits)) | (1 << (7 - Bits));

        private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
    }

    /// <summary>GIF の LZW 圧縮（可変長コード、最大 12 ビット、表がいっぱいになったらクリア）。</summary>
    private sealed class Lzw
    {
        private const int MaxCode = 4096;

        // (前の符号 << 8 | 次の番号) → 符号。表は世代番号で空にし、フレームをまたいで使い回す
        private readonly int[] _table = new int[MaxCode << 8];
        private readonly int[] _generations = new int[MaxCode << 8];
        private int _generation;

        public void Write(Stream stream, byte[] pixels, int minCodeSize)
        {
            stream.WriteByte((byte)minCodeSize);
            var output = new BitWriter(stream);

            int clear = 1 << minCodeSize;
            int end = clear + 1;
            int codeSize = minCodeSize + 1;
            int next = end + 1;
            int generation = ++_generation;

            output.Write(clear, codeSize);
            int prefix = pixels[0];
            for (int i = 1; i < pixels.Length; i++)
            {
                int key = (prefix << 8) | pixels[i];
                if (_generations[key] == generation)
                {
                    prefix = _table[key];
                    continue;
                }

                output.Write(prefix, codeSize);
                if (next >= (1 << codeSize) && codeSize < 12)
                    codeSize++;
                if (next < MaxCode)
                {
                    _table[key] = next++;
                    _generations[key] = generation;
                }
                else
                {
                    output.Write(clear, codeSize);
                    codeSize = minCodeSize + 1;
                    next = end + 1;
                    generation = ++_generation;
                }
                prefix = pixels[i];
            }
            output.Write(prefix, codeSize);
            output.Write(end, codeSize);
            output.Flush();
        }

        /// <summary>符号を下位ビットから詰め、255 バイトずつのサブブロックにして書く。</summary>
        private sealed class BitWriter(Stream stream)
        {
            private readonly byte[] _block = new byte[255];
            private int _blockLength;
            private int _bits;
            private int _bitCount;

            public void Write(int code, int size)
            {
                _bits |= code << _bitCount;
                _bitCount += size;
                while (_bitCount >= 8)
                {
                    Add((byte)_bits);
                    _bits >>= 8;
                    _bitCount -= 8;
                }
            }

            public void Flush()
            {
                if (_bitCount > 0)
                    Add((byte)_bits);
                if (_blockLength > 0)
                    WriteBlock();
                stream.WriteByte(0); // ブロックの終わり
            }

            private void Add(byte value)
            {
                _block[_blockLength++] = value;
                if (_blockLength == _block.Length)
                    WriteBlock();
            }

            private void WriteBlock()
            {
                stream.WriteByte((byte)_blockLength);
                stream.Write(_block, 0, _blockLength);
                _blockLength = 0;
            }
        }
    }
}

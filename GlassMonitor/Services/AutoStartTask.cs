using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using GlassMonitor.Localization;
using Microsoft.CSharp.RuntimeBinder;

namespace GlassMonitor.Services;

/// <summary>自動起動の登録・解除に失敗した。</summary>
public sealed class AutoStartException(string message, Exception inner) : Exception(message, inner);

/// <summary>
/// サインイン時の自動起動。アプリは管理者権限で動くため、レジストリの Run キーでは起動されない
/// （昇格が必要なアプリは Windows がスキップする）。代わりにタスク スケジューラに
/// 「最上位の特権で実行」のログオン タスクを登録する。
/// </summary>
internal static class AutoStartTask
{
    public const string TaskName = "GlassMonitor";

    private const int TASK_CREATE_OR_UPDATE = 6;
    private const int TASK_LOGON_INTERACTIVE_TOKEN = 3;

    /// <summary>このアプリ（現在の exe パス）を起動するタスクが登録されているか。調べられないときは false。</summary>
    public static bool IsRegistered()
    {
        try
        {
            dynamic? task = FindTask();
            if (task is null)
                return false;

            dynamic actions = task.Definition.Actions;
            for (int i = 1; i <= (int)actions.Count; i++)
            {
                string path = ((string)actions.Item(i).Path).Trim('"');
                if (string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch (Exception ex) when (IsSchedulerError(ex))
        {
            return false;
        }
    }

    /// <summary>登録する（同名のタスクがあれば上書き）。</summary>
    public static void Register()
    {
        try
        {
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException(Localizer.Get("AutoStart_ExePathUnavailable"));
            dynamic folder = Connect().GetFolder(@"\");
            folder.RegisterTask(TaskName, BuildXml(exe), TASK_CREATE_OR_UPDATE, null, null, TASK_LOGON_INTERACTIVE_TOKEN, null);
        }
        catch (Exception ex) when (IsSchedulerError(ex) || ex is InvalidOperationException)
        {
            throw new AutoStartException(ex.Message, ex);
        }
    }

    /// <summary>登録を解除する。タスクが無ければ何もしない。</summary>
    public static void Unregister()
    {
        try
        {
            if (FindTask() is null)
                return;
            dynamic folder = Connect().GetFolder(@"\");
            folder.DeleteTask(TaskName, 0);
        }
        catch (Exception ex) when (IsSchedulerError(ex))
        {
            throw new AutoStartException(ex.Message, ex);
        }
    }

    /// <summary>タスクが無ければ null。COM の HRESULT 0x80070002 は FileNotFoundException として届く。</summary>
    private static dynamic? FindTask()
    {
        try
        {
            return Connect().GetFolder(@"\").GetTask(TaskName);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static dynamic Connect()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new COMException(Localizer.Get("AutoStart_TaskSchedulerUnavailable"));
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    // COM の HRESULT は内容に応じて COMException / IOException / UnauthorizedAccessException などに変換される
    private static bool IsSchedulerError(Exception ex) =>
        ex is COMException or IOException or UnauthorizedAccessException or RuntimeBinderException;

    private static string BuildXml(string exe)
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        string command = SecurityElement.Escape($"\"{exe}\"");
        string directory = SecurityElement.Escape(Path.GetDirectoryName(exe) ?? "");
        string description = SecurityElement.Escape(Localizer.Get("AutoStart_TaskDescription"));

        // 電源やアイドルの条件で止まらないようにし、実行時間の上限（既定 72 時間）も外す
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>GlassMonitor</Author>
                <Description>{description}</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                  <Delay>PT10S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>5</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                  <WorkingDirectory>{directory}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }
}

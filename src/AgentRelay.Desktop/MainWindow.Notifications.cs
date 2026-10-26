using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private string? notificationSetupHint;
    private Window? activeNotificationWindow;
    private string CurrentNotifierPath()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","desktop","AgentRelay.Notify.exe"));

    private void InitializeNotificationDelivery()
    {
        try
        {
            service.RebindDesktopExecutable(CurrentNotifierPath());
            if(preferences.LastNotificationId is null)
                SaveNotificationCursor(service.Inbox().Select(n=>n.NotificationId).DefaultIfEmpty().Max());
        }
        catch(Exception error) when(error is RelayException or IOException or UnauthorizedAccessException)
        {notificationSetupHint=T("Desktop notification unavailable")+": "+error.Message;}
    }

    private void SaveNotificationCursor(long id)
    {
        var next=preferences with {LastNotificationId=id};Appearance.Save(service.Home,next);preferences=next;
    }

    private async Task<string?> DeliverPendingDesktopNotificationsAsync()
    {
        try
        {
            if(preferences.LastNotificationId is null)InitializeNotificationDelivery();
            var previous=preferences.LastNotificationId??0;
            var pending=service.Inbox().Where(n=>n.NotificationId>previous).OrderBy(n=>n.NotificationId).ToArray();
            if(pending.Length==0)return notificationSetupHint;
            // Claim this batch before external delivery. Unknown process results must not cause duplicate pop-ups after refresh.
            SaveNotificationCursor(pending[^1].NotificationId);
            if(service.DesktopExecutable() is null)return null;
            var tasks=service.Tasks().ToDictionary(v=>v.Task.TaskId,v=>v.Task,StringComparer.Ordinal);
            string? hint=notificationSetupHint;
            foreach(var item in pending.GroupBy(n=>n.TaskId).Select(g=>g.Last()))
            {
                if(!tasks.TryGetValue(item.TaskId,out var task))continue; // A user may have deleted a task before its desktop event was handled.
                var result=await new DesktopNotifier(service.DesktopExecutable(),muteAudio:true,suppressDisplay:true).NotifyAsync(service.Home,task,CancellationToken.None);
                ShowApplicationNotification(service.NotificationContent(task),task.TaskId);
                PlayNotificationSound(preferences.NotificationSound);
                if(result.State!="submitted")
                    hint=T("Desktop notification unavailable")+": "+result.Detail+
                        " · "+T("An in-app notification was shown instead.");
            }
            return hint;
        }
        catch(Exception error) when(error is RelayException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {return T("Desktop notification unavailable")+": "+error.Message;}
    }

    private void ShowApplicationNotification(DesktopNotificationContent content,string? targetTask=null)
    {
        activeNotificationWindow?.Close();
        var popup=new ToastNotificationWindow(this,content,T("In-app notification"),T("Close"),
            targetTask is null?null:T("Open task"),targetTask is null?null:()=>{taskId=targetTask;Go("Home");OpenFromTray();});
        var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(9)};
        timer.Tick+=(_,_)=>popup.Close();
        popup.MouseEnter+=(_,_)=>timer.Stop();popup.MouseLeave+=(_,_)=>timer.Start();
        void OwnerClosed(object? sender,EventArgs args)=>popup.Close();
        Closed+=OwnerClosed;
        popup.Closed+=(_,_)=>{timer.Stop();Closed-=OwnerClosed;if(activeNotificationWindow==popup)activeNotificationWindow=null;};
        activeNotificationWindow=popup;popup.Show();timer.Start();
    }

    private static void PlayNotificationSound(string sound)
        =>NotificationSounds.Play(sound);

    private void AddNotificationSoundSettings(Panel page)
    {
        page.Children.Add(Note("Notification sound"));
        var row=new WrapPanel();page.Children.Add(row);
        var choice=new ComboBox{Name="NotificationSoundChoice",MinWidth=180,MinHeight=34,DisplayMemberPath="Label",SelectedValuePath="Id",
            ItemsSource=new[]{new AppearanceOption("none",T("Silent")),new AppearanceOption("default",T("Default sound")),new AppearanceOption("information",T("Information sound")),new AppearanceOption("warning",T("Warning sound")),new AppearanceOption("error",T("Error sound"))},SelectedValue=preferences.NotificationSound};
        AutomationProperties.SetAutomationId(choice,choice.Name);row.Children.Add(choice);
        choice.SelectionChanged+=async(_,_)=>
        {
            if(choice.SelectedValue is not string sound||sound==preferences.NotificationSound)return;
            await Run(()=>{var next=preferences with {NotificationSound=sound};Appearance.Save(service.Home,next);preferences=next;return Task.CompletedTask;},"Notification sound saved",refresh:false);
        };
        Add(row,"Preview sound",()=>{PlayNotificationSound((string)choice.SelectedValue);return Task.CompletedTask;},false);
        Add(row,"Send test notification",async()=>
        {
            var content=new DesktopNotificationContent("Agent Relay · "+T("Test notification"),T("This tests desktop notifications only. No task or model session was created."));
            var result=await new DesktopNotifier(File.Exists(CurrentNotifierPath())?CurrentNotifierPath():service.DesktopExecutable(),muteAudio:true,suppressDisplay:true)
                .TestAsync(service.Home,content.Title,content.Message);
            if(result.State=="submitted")
            {ShowApplicationNotification(content);PlayNotificationSound(preferences.NotificationSound);notificationSetupHint=null;}
            else
            {
                ShowApplicationNotification(content);PlayNotificationSound(preferences.NotificationSound);
                notificationSetupHint=T("Desktop notification unavailable")+": "+result.Detail+" · "+T("An in-app notification was shown instead.");
            }
            LedgerHint.Text=result.State=="submitted"?T("The in-app reminder was shown and Windows accepted a silent notification-center entry."):"";
        },false);
        page.Children.Add(Note("Sounds follow Windows volume settings. Notification banners also depend on Windows notification and Do not disturb settings."));
    }
}

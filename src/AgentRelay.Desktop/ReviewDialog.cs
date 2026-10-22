using System.Windows;
using System.Windows.Controls;

namespace AgentRelay.Desktop;

public sealed class ReviewDialog : Window
{
    public string Reason { get; private set; } = "";

    public ReviewDialog(Window owner, string title, string preview)
    {
        Owner = owner; Title = title; Width = 750; Height = 590; MinWidth = 550; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = owner.FontFamily; FontSize = 13; Resources = owner.Resources;
        var panel = new DockPanel { Margin = new Thickness(20) };
        Content = panel;
        var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); panel.Children.Add(bottom);
        bottom.Children.Add(new TextBlock { Text = "Approval reason", Margin = new Thickness(0,12,0,0) });
        var reason = new TextBox { Name = "ApprovalReason" }; bottom.Children.Add(reason);
        var check = new CheckBox { Name = "ApprovalCheck", Content = "我已审阅目标、具体变更和影响，批准本次操作。", Margin = new Thickness(0,0,0,12) };
        bottom.Children.Add(check);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; bottom.Children.Add(buttons);
        var approve = new Button { Name = "ApproveButton", Content = "Approve", IsEnabled = false, IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true }; buttons.Children.Add(approve); buttons.Children.Add(cancel);
        reason.TextChanged += (_, _) => approve.IsEnabled = check.IsChecked == true && !string.IsNullOrWhiteSpace(reason.Text);
        check.Checked += (_, _) => approve.IsEnabled = !string.IsNullOrWhiteSpace(reason.Text);
        check.Unchecked += (_, _) => approve.IsEnabled = false;
        approve.Click += (_, _) => { Reason = reason.Text.Trim(); DialogResult = true; };
        panel.Children.Add(new TextBox { Text = preview, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new("Consolas") });
    }
}

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AgentRelay.Desktop;

public sealed class CloseBehaviorDialog : Window
{
    public bool CloseToTray { get; private set; }

    public CloseBehaviorDialog(Window owner, string language)
    {
        string T(string key) => CloseBehaviorText.Get(key, language);
        Owner = owner; Title = T("title"); Width = 490; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = owner.FontFamily; FontSize = owner.FontSize; Resources = owner.Resources;
        Background = (Brush)owner.FindResource("PageBrush"); Foreground = (Brush)owner.FindResource("TextBrush");
        var body = new StackPanel { Margin = new(24) }; Content = body;
        body.Children.Add(new TextBlock { Text = T("question"), FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = T("remember"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 18), Foreground = (Brush)owner.FindResource("MutedBrush") });
        var actions = new WrapPanel(); body.Children.Add(actions);
        Add("tray", "CloseToTrayChoice", true);
        Add("exit", "ExitApplicationChoice", false);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };

        void Add(string key, string name, bool tray)
        {
            var button = new Button { Name = name, Content = T(key), MinHeight = 38, Height = double.NaN, IsDefault = tray };
            AutomationProperties.SetAutomationId(button, name);
            if (tray) button.SetResourceReference(StyleProperty, "PrimaryButtonStyle");
            button.Click += (_, _) => { CloseToTray = tray; DialogResult = true; };
            actions.Children.Add(button);
        }
    }
}

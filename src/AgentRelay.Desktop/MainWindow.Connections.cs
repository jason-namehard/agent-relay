using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    /// <summary>The graph on the left; the selected pair's two directions, each with its own switch, on the right.</summary>
    private FrameworkElement ConnectionsPage()
    {
        avatarSlots = null;
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new(16) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new(340) });
        var edges = service.Connections();
        var side = new StackPanel();
        var sideScroll = new ScrollViewer { Content = side, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(sideScroll, 2); layout.Children.Add(sideScroll);
        void Select(ConnectionView edge)
        {
            connectionFrom = edge.From; connectionTo = edge.To; side.Children.Clear();
            var directions = new StackPanel();
            side.Children.Add(Card(directions));
            foreach (var direction in new[] { edge, edges.Single(e => e.From == edge.To && e.To == edge.From) })
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var frame = new Border { Child = row, Padding = new(16, 14, 16, 14), BorderThickness = new(0, directions.Children.Count == 0 ? 0 : 1, 0, 0) };
                frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                directions.Children.Add(frame);
                var route = RouteLine(direction.From, direction.To, 20, 13); route.Margin = new(0, 0, 10, 0); row.Children.Add(route);
                var explanation = Muted(direction.Display == "Unavailable" ? "No calling recipe. Copy a handoff prompt, or try an advanced CLI extension." :
                    direction.Display == "Allowed" ? "Automatic notification is enabled for this direction." : "A calling recipe is available; this direction is paused or not enabled.", 12.5, new(0, 8, 0, 0));
                Grid.SetRow(explanation, 1); Grid.SetColumnSpan(explanation, 2); row.Children.Add(explanation);
                var name = (direction.Display == "Unavailable" ? "ExtendConnection" : direction.Display == "Allowed" ? "PauseConnection" : "EnableConnection") + "_" +
                    direction.From.Replace('-', '_') + "_" + direction.To.Replace('-', '_');
                if (direction.Display != "Unavailable")
                {
                    var label = direction.Display == "Allowed" ? "Pause connection" : "Enable connection";
                    var toggle = Add(new StackPanel(), label, () => { service.SetPermission(direction.From, direction.To, direction.Display == "Allowed" ? "deny" : "allow"); return Task.CompletedTask; });
                    ((Panel)toggle.Parent).Children.Remove(toggle);
                    toggle.SetResourceReference(StyleProperty, "SwitchButtonStyle");
                    toggle.Content = null; toggle.ToolTip = T(label);
                    toggle.Name = name; AutomationProperties.SetAutomationId(toggle, name);
                    AutomationProperties.SetName(toggle, T(label) + ": " + AgentName(direction.From) + " → " + AgentName(direction.To));
                    Grid.SetColumn(toggle, 1); row.Children.Add(toggle);
                }
                else
                {
                    var pill = Pill(GraphText.State("Unavailable", preferences.Language), "off");
                    Grid.SetColumn(pill, 1); row.Children.Add(pill);
                    var actions = new WrapPanel { Margin = new(0, 10, 0, -8) };
                    Grid.SetRow(actions, 2); Grid.SetColumnSpan(actions, 2); row.Children.Add(actions);
                    var target = direction.To;
                    var extend = Add(actions, "Agent bridges", () => { agentId = target; Go("Agents"); return Task.CompletedTask; });
                    extend.Content = Icons.WithLabel("Bridge", string.Format(System.Globalization.CultureInfo.CurrentUICulture, T("Set up a bridge for {0}"), AgentName(target)));
                    extend.Name = name; AutomationProperties.SetAutomationId(extend, name);
                }
            }
            side.Children.Add(Muted("Switches only control Relay's automatic delivery. Copying a prompt by hand always works.", 12.5, new(2, 10, 2, 6)));
            Styled(Add(Row(side), "Manual handoff", () => { Go("Home"); return Task.CompletedTask; }), "LinkButtonStyle");
        }
        var graph = new ConnectionGraph(service.Agents(), edges, Select, preferences.Language, this, AgentName, service.CliExtensions().ToDictionary(e => e.AgentId), AvatarKey);
        layout.Children.Add(Card(graph, new Thickness(14)));
        var selected = edges.SingleOrDefault(e => e.From == connectionFrom && e.To == connectionTo) ?? edges.FirstOrDefault();
        if (selected is not null) Select(selected);
        UpdateSidebar();
        return layout;
    }
}

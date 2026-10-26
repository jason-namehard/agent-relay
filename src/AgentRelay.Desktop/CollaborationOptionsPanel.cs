using System.Globalization;
using System.Windows.Automation;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

/// <summary>How a new handoff proceeds: one pass, or revise-and-review until the reviewer passes it and the author accepts.</summary>
internal sealed class CollaborationOptionsPanel
{
    private readonly EntryDialogLayout ui;
    private readonly RadioButton loop;
    private readonly RadioButton senderReviews;
    private readonly TextBox criteria;
    private readonly TextBox rounds;
    private readonly TextBox turnMinutes;
    private readonly CheckBox automatic;

    public CollaborationOptionsPanel(EntryDialogLayout ui, Panel parent, string prefix, int defaultTurnMinutes, bool automaticByDefault)
    {
        this.ui = ui;
        RadioButton Choice(string text, string name, string group, bool selected)
        {
            var choice = new RadioButton { Name = prefix + name, Content = ui.T(text), GroupName = prefix + group, IsChecked = selected, Margin = new(0, 6, 0, 6) };
            AutomationProperties.SetAutomationId(choice, prefix + name);
            return choice;
        }
        parent.Children.Add(ui.Note("Collaboration"));
        var single = Choice("Single handoff", "Single", "Mode", true);
        loop = Choice("Keep revising until the review passes", "Loop", "Mode", false);
        parent.Children.Add(single); parent.Children.Add(loop);
        var details = new StackPanel { Margin = new(18, 4, 0, 8), IsEnabled = false };
        parent.Children.Add(details);
        details.Children.Add(ui.Note("The reviewer checks each version. The task finishes only after the reviewer passes it and the author accepts and records the deliverable."));
        senderReviews = Choice("Recipient does the work; sender reviews", "SenderReviews", "Roles", true);
        var recipientReviews = Choice("Sender already has the work; recipient reviews", "RecipientReviews", "Roles", false);
        details.Children.Add(senderReviews); details.Children.Add(recipientReviews);
        criteria = ui.Field(details, "Acceptance criteria", prefix + "Criteria", multiline: true); criteria.Height = 70;
        rounds = ui.Field(details, "Maximum review rounds", prefix + "Rounds", "5");
        loop.Checked += (_, _) => details.IsEnabled = true;
        single.Checked += (_, _) => details.IsEnabled = false;
        turnMinutes = ui.Field(parent, "Time limit per turn (minutes)", prefix + "TurnMinutes", defaultTurnMinutes.ToString(CultureInfo.InvariantCulture));
        automatic = new CheckBox { Name = prefix + "AutoDeliver", IsChecked = automaticByDefault, Margin = new(0, 8, 0, 8),
            Content = new TextBlock { Text = ui.T("Send each next turn through CLI automatically once that Agent's CLI is verified (may incur model charges)"), TextWrapping = System.Windows.TextWrapping.Wrap } };
        AutomationProperties.SetAutomationId(automatic, prefix + "AutoDeliver");
        parent.Children.Add(automatic);
    }

    public bool AutoDeliver => automatic.IsChecked == true;

    public FileHandoffOptions Build(string sender, string recipient)
    {
        int Number(TextBox box, int min, int max)
        {
            if (!int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
                throw new RelayException("handoff_number_invalid", string.Format(CultureInfo.CurrentUICulture, ui.T("Enter a whole number from {0} to {1}."), min, max));
            return value;
        }
        var minutes = Number(turnMinutes, 1, 43200);
        var collaboration = loop.IsChecked == true
            ? new CollaborationOptions(senderReviews.IsChecked == true ? recipient : sender, criteria.Text, Number(rounds, 1, RelayService.MaxReviewRounds))
            : null;
        return new(collaboration, AutoDeliver, minutes);
    }
}

using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private readonly HashSet<string> selectedTaskIds = [];

    private sealed class TaskRow(string taskId,string route,string mode,string state,DateTimeOffset created,string stateCode)
        : INotifyPropertyChanged
    {
        public string TaskId { get; }=taskId;
        public string Route { get; }=route;
        public string Mode { get; }=mode;
        public string State { get; }=state;
        public DateTimeOffset Created { get; }=created;
        public string StateCode { get; }=stateCode;
        private bool batchSelected;
        public bool BatchSelected
        {
            get => batchSelected;
            set
            {
                if (batchSelected == value) return;
                batchSelected = value;
                PropertyChanged?.Invoke(this,new(nameof(BatchSelected)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private void AddTaskBatchControls(Panel page,DataGrid table,TaskRow[] rows)
    {
        selectedTaskIds.IntersectWith(rows.Select(row=>row.TaskId));
        foreach (var row in rows) row.BatchSelected=selectedTaskIds.Contains(row.TaskId);

        var actions=Row(page);actions.Name="TaskBatchActions";
        AutomationProperties.SetAutomationId(actions,actions.Name);
        var all=Add(actions,"Select all tasks",()=>
        {
            foreach(var row in rows)row.BatchSelected=true;
            return Task.CompletedTask;
        },false);
        all.Name="SelectAllTasks";AutomationProperties.SetAutomationId(all,all.Name);
        var count=new TextBlock{Name="TaskBatchCount",VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,12,0)};
        actions.Children.Add(count);
        var complete=Add(actions,"Mark selected complete",()=>ApplyTaskBatch(rows,false));
        complete.Name="CompleteSelectedTasks";AutomationProperties.SetAutomationId(complete,complete.Name);
        var delete=Add(actions,"Delete selected tasks",()=>ApplyTaskBatch(rows,true));
        delete.Name="DeleteSelectedTasks";AutomationProperties.SetAutomationId(delete,delete.Name);
        var clear=Add(actions,"Clear selection",()=>
        {
            foreach (var row in rows) row.BatchSelected=false;
            return Task.CompletedTask;
        },false);
        clear.Name="ClearTaskSelection";AutomationProperties.SetAutomationId(clear,clear.Name);

        void UpdateActions()
        {
            actions.Visibility=rows.Length==0?Visibility.Collapsed:Visibility.Visible;
            all.IsEnabled=selectedTaskIds.Count<rows.Length;
            count.Visibility=complete.Visibility=delete.Visibility=selectedTaskIds.Count==0?Visibility.Collapsed:Visibility.Visible;
            count.Text=string.Format(CultureInfo.CurrentUICulture,T("Selected: {0}"),selectedTaskIds.Count);
            complete.IsEnabled=rows.Any(row=>row.BatchSelected&&row.StateCode!="Succeeded");
            clear.Visibility=selectedTaskIds.Count>=2?Visibility.Visible:Visibility.Collapsed;
        }
        foreach (var row in rows)
            row.PropertyChanged+=(_,_)=>
            {
                if (row.BatchSelected) selectedTaskIds.Add(row.TaskId); else selectedTaskIds.Remove(row.TaskId);
                UpdateActions();
            };
        UpdateActions();

        var checkbox=new FrameworkElementFactory(typeof(CheckBox));
        checkbox.SetValue(FrameworkElement.MarginProperty,new Thickness(0));
        checkbox.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
        checkbox.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        checkbox.SetValue(FrameworkElement.ToolTipProperty,T("Select this task for batch actions"));
        checkbox.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(TaskRow.BatchSelected)){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
        checkbox.SetBinding(AutomationProperties.AutomationIdProperty,new Binding(nameof(TaskRow.TaskId)){StringFormat="SelectTask_{0}"});
        checkbox.SetBinding(AutomationProperties.NameProperty,new Binding(nameof(TaskRow.Route)){StringFormat=T("Select task: {0}")});
        // Checking a task changes the batch selection, without replacing the task being inspected.
        checkbox.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((sender,args)=>
        {
            var control=(CheckBox)sender;
            control.SetCurrentValue(CheckBox.IsCheckedProperty,control.IsChecked!=true);
            args.Handled=true;
        }));
        var cellStyle=new Style(typeof(DataGridCell),(Style)FindResource(typeof(DataGridCell)));
        cellStyle.Setters.Add(new Setter(AutomationProperties.NameProperty,new Binding(nameof(TaskRow.Route)){StringFormat=T("Select task: {0}")}));
        table.Columns.Insert(0,new DataGridTemplateColumn{Header=T("Select"),Width=54,CanUserResize=false,CanUserSort=false,
            CellStyle=cellStyle,CellTemplate=new DataTemplate{VisualTree=checkbox}});
    }

    private Task ApplyTaskBatch(TaskRow[] rows,bool delete)
    {
        var selected=rows.Where(row=>row.BatchSelected).ToArray();
        var title=T(delete?"Delete selected tasks":"Mark selected complete");
        var question=string.Format(CultureInfo.CurrentUICulture,T(delete?"Delete these {0} selected tasks?":"Mark these {0} selected tasks complete?"),selected.Length);
        var explanation=T(delete
            ?"Delete this task and its history? Files will be kept; unregistered relay files remain eligible for retention cleanup. This does not stop an already running Agent. This action cannot be undone."
            :"Mark this handoff complete yourself? Relay will stop waiting and record your decision. This does not verify the Agent's work or stop an already running Agent.");
        var targets=string.Join("\n",selected.Select(row=>row.TaskId+" · "+row.Route));
        if (MessageBox.Show(this,question+"\n\n"+targets+"\n\n"+explanation,title,MessageBoxButton.OKCancel,
                delete?MessageBoxImage.Warning:MessageBoxImage.Question)!=MessageBoxResult.OK)
        {reviewCancelled=true;return Task.CompletedTask;}
        foreach (var row in selected)
        {
            if (delete) service.DeleteTask(row.TaskId,true);
            else service.ForceCompleteTask(row.TaskId,"Marked complete by user",true);
            row.BatchSelected=false;
            if (delete&&taskId==row.TaskId) taskId="";
        }
        return Task.CompletedTask;
    }
}

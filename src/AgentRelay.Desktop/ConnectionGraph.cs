using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public sealed class ConnectionGraph : Viewbox
{
    public ConnectionGraph(IReadOnlyList<AgentRecord> agents,IReadOnlyList<ConnectionView> edges,Action<ConnectionView> select)
    {
        Stretch=Stretch.Uniform; HorizontalAlignment=HorizontalAlignment.Left; MaxHeight=290; Margin=new(0,0,0,12);
        var canvas=new Canvas{Width=820,Height=280,Background=Brushes.White};Child=canvas;
        if(agents.Count==0){canvas.Children.Add(new TextBlock{Text="Register agents to view directed connections.",Margin=new(20)});return;}
        var positions=agents.Select((a,i)=>(a.AgentId,Point:new Point(410+310*Math.Cos(-Math.PI/2+2*Math.PI*i/agents.Count),140+100*Math.Sin(-Math.PI/2+2*Math.PI*i/agents.Count))))
            .ToDictionary(p=>p.AgentId,p=>p.Point);
        foreach(var edge in edges.Where(e=>agents.Count<=8 || e.Permission!="default_deny"))
        {
            var a=positions[edge.From];var b=positions[edge.To];var delta=b-a;delta.Normalize();
            var start=a+delta*34;var end=b-delta*34;var normal=new Vector(-delta.Y,delta.X);
            var control=new Point((start.X+end.X)/2,(start.Y+end.Y)/2)+normal*25;
            var geometry=new PathGeometry([new PathFigure(start,[new QuadraticBezierSegment(control,end,true)],false)]);
            var color=edge.Display switch {"Allowed"=>Brushes.SeaGreen,"Denied"=>Brushes.Firebrick,_=>Brushes.SlateGray};
            var path=new System.Windows.Shapes.Path{Data=geometry,Stroke=color,StrokeThickness=2,Cursor=Cursors.Hand,
                ToolTip=$"{edge.From} → {edge.To}\n{edge.Display}\nPermission: {edge.Permission}\nCapability: {edge.Capability}\n{edge.Reason}"};
            if(edge.Permission=="default_deny")path.StrokeDashArray=new DoubleCollection([4,4]);
            path.MouseLeftButtonDown+=(_,_)=>select(edge);canvas.Children.Add(path);
            var tangent=end-control;tangent.Normalize();var side=new Vector(-tangent.Y,tangent.X);
            canvas.Children.Add(new Polygon{Points=new PointCollection([end,end-tangent*10+side*4,end-tangent*10-side*4]),Fill=color,IsHitTestVisible=false});
        }
        foreach(var agent in agents)
        {
            var p=positions[agent.AgentId];var node=new Border{Width=130,Padding=new(8,6,8,6),Background=Brushes.White,
                BorderBrush=Brushes.SlateGray,BorderThickness=new(1),CornerRadius=new(5),ToolTip=$"{agent.ConfigPath}\n{agent.ValidationStatus}"};
            node.Child=new TextBlock{Text=agent.AgentId,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
            Canvas.SetLeft(node,p.X-65);Canvas.SetTop(node,p.Y-16);canvas.Children.Add(node);
        }
        if(agents.Count>8)canvas.Children.Add(new TextBlock{Text="Graph: explicit routes. Table: all directions + default deny.",Foreground=Brushes.SlateGray,Margin=new(8)});
    }
}

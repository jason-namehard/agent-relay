using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public sealed class ConnectionGraph : Viewbox
{
    public ConnectionGraph(IReadOnlyList<AgentRecord> agents,IReadOnlyList<ConnectionView> edges,Action<ConnectionView> select,string language,Window owner)
    {
        string T(string text)=>UiText.Translate(text,language);
        Brush B(string key)=>(Brush)owner.FindResource(key);
        Stretch=Stretch.Uniform; HorizontalAlignment=HorizontalAlignment.Left; MaxHeight=320; Margin=new(0,0,0,12);
        AutomationProperties.SetAutomationId(this,"ConnectionGraph");
        var explicitOnly=agents.Count>=4;
        var canvas=new Canvas{Width=820,Height=explicitOnly?320:280,Background=B("SurfaceBrush")};Child=canvas;
        if(agents.Count==0){canvas.Children.Add(new TextBlock{Text=T("Register agents to view directed connections."),Margin=new(20)});return;}
        var positions=agents.Select((a,i)=>(a.AgentId,Point:new Point(410+310*Math.Cos(-Math.PI/2+2*Math.PI*i/agents.Count),140+100*Math.Sin(-Math.PI/2+2*Math.PI*i/agents.Count))))
            .ToDictionary(p=>p.AgentId,p=>p.Point);
        foreach(var edge in edges.Where(e=>!explicitOnly || e.Permission!="default_deny"))
        {
            var a=positions[edge.From];var b=positions[edge.To];var delta=b-a;delta.Normalize();
            var normal=new Vector(-delta.Y,delta.X);
            var control=new Point((a.X+b.X)/2,(a.Y+b.Y)/2)+normal*42;
            var start=NodeBoundary(a,control-a);var end=NodeBoundary(b,control-b);
            var geometry=new PathGeometry([new PathFigure(start,[new QuadraticBezierSegment(control,end,true)],false)]);
            var color=edge.Display switch {"Allowed"=>B("AllowedBrush"),"Denied"=>B("DeniedBrush"),_=>B("UnavailableBrush")};
            var tooltip=$"{edge.From} → {edge.To}\n{T(edge.Display)}\n{T("Permission")}: {T(edge.Permission)}\n{T("Capability")}: {T(edge.Capability)}\n{T(edge.Reason)}";
            var hitPath=new System.Windows.Shapes.Path{Data=geometry,Stroke=Brushes.Transparent,StrokeThickness=14,
                Cursor=Cursors.Hand,ToolTip=tooltip,Tag=edge,Focusable=true};
            AutomationProperties.SetAutomationId(hitPath,$"ConnectionEdge:{edge.From}:{edge.To}");
            AutomationProperties.SetName(hitPath,$"{edge.From} → {edge.To}: {T(edge.Display)}");
            hitPath.MouseLeftButtonDown+=(_,e)=>{select(edge);e.Handled=true;};
            hitPath.KeyDown+=(_,e)=>{if(e.Key is Key.Enter or Key.Space){select(edge);e.Handled=true;}};
            canvas.Children.Add(hitPath);
            var path=new System.Windows.Shapes.Path{Data=geometry,Stroke=color,StrokeThickness=2,IsHitTestVisible=false};
            if(edge.Permission=="default_deny")path.StrokeDashArray=new DoubleCollection([4,4]);
            canvas.Children.Add(path);
            var tangent=end-control;tangent.Normalize();var side=new Vector(-tangent.Y,tangent.X);
            canvas.Children.Add(new Polygon{Points=new PointCollection([end,end-tangent*10+side*4,end-tangent*10-side*4]),Fill=color,IsHitTestVisible=false});
        }
        foreach(var agent in agents)
        {
            var p=positions[agent.AgentId];var node=new Border{Width=130,Height=38,Padding=new(8,6,8,6),Background=B("SurfaceAltBrush"),
                BorderBrush=B("BorderBrush"),BorderThickness=new(1),CornerRadius=new(12),ToolTip=$"{agent.ConfigPath}\n{T(agent.ValidationStatus)}"};
            AutomationProperties.SetAutomationId(node,$"ConnectionNode:{agent.AgentId}");
            node.Child=new TextBlock{Text=agent.AgentId,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
            Canvas.SetLeft(node,p.X-65);Canvas.SetTop(node,p.Y-19);Panel.SetZIndex(node,1);canvas.Children.Add(node);
        }
        if(explicitOnly)
        {
            var note=new TextBlock{Text=T("Graph: explicit routes. Table: all directions + default deny."),
                Foreground=B("MutedBrush"),Width=796,TextWrapping=TextWrapping.Wrap};
            AutomationProperties.SetAutomationId(note,"ConnectionGraphSummary");
            Canvas.SetLeft(note,12);Canvas.SetTop(note,280);canvas.Children.Add(note);
        }
    }

    private static Point NodeBoundary(Point center,Vector direction)
    {
        var scale=Math.Min(71/Math.Abs(direction.X),25/Math.Abs(direction.Y));
        return center+direction*scale;
    }
}

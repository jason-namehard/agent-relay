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
    public ConnectionGraph(IReadOnlyList<AgentRecord> agents,IReadOnlyList<ConnectionView> edges,Action<ConnectionView> select,string language,Window owner,Func<string,string>? displayName=null)
    {
        string T(string text)=>UiText.Translate(text,language);
        string Name(string id)=>displayName is null?id:displayName(id);
        Brush B(string key)=>(Brush)owner.FindResource(key);
        Stretch=Stretch.Uniform; StretchDirection=StretchDirection.DownOnly; HorizontalAlignment=HorizontalAlignment.Left; Margin=new(0,0,0,12);
        AutomationProperties.SetAutomationId(this,"ConnectionGraph");
        var canvas=new Canvas{Width=820,Height=380,Background=B("SurfaceBrush")};Child=canvas;
        if(agents.Count==0){canvas.Children.Add(new TextBlock{Text=T("Register agents to view directed connections."),Margin=new(20)});return;}
        var positions=agents.Select((a,i)=>(a.AgentId,Point:new Point(410+310*Math.Cos(-Math.PI/2+2*Math.PI*i/agents.Count),190+145*Math.Sin(-Math.PI/2+2*Math.PI*i/agents.Count))))
            .ToDictionary(p=>p.AgentId,p=>p.Point);
        foreach(var edge in edges)
        {
            var a=positions[edge.From];var b=positions[edge.To];var delta=b-a;delta.Normalize();
            var normal=new Vector(-delta.Y,delta.X);
            var control=new Point((a.X+b.X)/2,(a.Y+b.Y)/2)+normal*110;
            var start=NodeBoundary(a,control-a);var end=NodeBoundary(b,control-b);
            var geometry=new PathGeometry([new PathFigure(start,[new QuadraticBezierSegment(control,end,true)],false)]);
            var color=edge.Display switch {"Allowed"=>B("AllowedBrush"),"Denied"=>B("DeniedBrush"),"Unavailable"=>B("UnavailableBrush"),_=>throw new ArgumentOutOfRangeException(nameof(edges),edge.Display,"Unknown connection display state.")};
            var state=StateText(edge.Display,language);
            var tooltip=$"{Name(edge.From)} → {Name(edge.To)}\n{state}\n{T(edge.Reason)}";
            var hitPath=new System.Windows.Shapes.Path{Data=geometry,Stroke=Brushes.Transparent,StrokeThickness=18,
                Cursor=Cursors.Hand,ToolTip=tooltip,Tag=edge,Focusable=true};
            AutomationProperties.SetAutomationId(hitPath,$"ConnectionEdge:{edge.From}:{edge.To}");
            AutomationProperties.SetName(hitPath,$"{Name(edge.From)} → {Name(edge.To)}: {state}");
            hitPath.MouseLeftButtonDown+=(_,e)=>{select(edge);e.Handled=true;};
            hitPath.KeyDown+=(_,e)=>{if(e.Key is Key.Enter or Key.Space){select(edge);e.Handled=true;}};
            canvas.Children.Add(hitPath);
            var path=new System.Windows.Shapes.Path{Data=geometry,Stroke=color,StrokeThickness=2.5,IsHitTestVisible=false};
            AutomationProperties.SetAutomationId(path,$"ConnectionStroke:{edge.From}:{edge.To}");
            if(edge.Display=="Unavailable")path.StrokeDashArray=new DoubleCollection([4,4]);
            canvas.Children.Add(path);
            if(edge.Display!="Unavailable")
            {
                var tangent=end-control;tangent.Normalize();var side=new Vector(-tangent.Y,tangent.X);
                var arrow=new Polygon{Points=new PointCollection([end,end-tangent*12+side*5,end-tangent*12-side*5]),Fill=color,IsHitTestVisible=false};
                AutomationProperties.SetAutomationId(arrow,$"ConnectionArrow:{edge.From}:{edge.To}");
                canvas.Children.Add(arrow);
            }
            var midpoint=new Point((start.X+2*control.X+end.X)/4,(start.Y+2*control.Y+end.Y)/4);
            var label=new Border{Background=B("SurfaceBrush"),CornerRadius=new(4),Padding=new(5,2,5,2),Cursor=Cursors.Hand,ToolTip=tooltip,
                Child=new TextBlock{Text=state,Foreground=color,FontSize=12}};
            AutomationProperties.SetAutomationId(label,$"ConnectionLabel:{edge.From}:{edge.To}");
            label.MouseLeftButtonDown+=(_,e)=>{select(edge);e.Handled=true;};
            label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            Canvas.SetLeft(label,midpoint.X-label.DesiredSize.Width/2);Canvas.SetTop(label,midpoint.Y-label.DesiredSize.Height/2);
            Panel.SetZIndex(label,1);canvas.Children.Add(label);
        }
        foreach(var agent in agents)
        {
            var p=positions[agent.AgentId];var name=Name(agent.AgentId);var node=new Border{Width=150,MinHeight=42,Padding=new(8,7,8,7),Background=B("SurfaceAltBrush"),
                BorderBrush=B("BorderBrush"),BorderThickness=new(1),CornerRadius=new(12),ToolTip=$"{name}\n{agent.AgentId}"};
            AutomationProperties.SetAutomationId(node,$"ConnectionNode:{agent.AgentId}");
            node.Child=new TextBlock{Text=name,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,Foreground=B("TextBrush")};
            Canvas.SetLeft(node,p.X-75);Canvas.SetTop(node,p.Y-21);Panel.SetZIndex(node,2);canvas.Children.Add(node);
        }
    }

    private static string StateText(string display,string language)=>(display,language) switch
    {
        ("Allowed","zh-CN")=>"已启用",("Denied","zh-CN")=>"已暂停",("Unavailable","zh-CN")=>"未建立连接",
        ("Allowed","zh-TW")=>"已啟用",("Denied","zh-TW")=>"已暫停",("Unavailable","zh-TW")=>"未建立連線",
        ("Allowed","ja")=>"有効",("Denied","ja")=>"一時停止",("Unavailable","ja")=>"未接続",
        ("Allowed","ko")=>"사용 중",("Denied","ko")=>"일시 중지",("Unavailable","ko")=>"연결되지 않음",
        ("Allowed","es")=>"Activa",("Denied","es")=>"En pausa",("Unavailable","es")=>"Sin conexión",
        ("Allowed","en")=>"Enabled",("Denied","en")=>"Paused",("Unavailable","en")=>"Not connected",
        _=>throw new ArgumentOutOfRangeException(nameof(language),language,"Unsupported connection label.")
    };

    private static Point NodeBoundary(Point center,Vector direction)
    {
        var scale=Math.Min(81/Math.Abs(direction.X),27/Math.Abs(direction.Y));
        return center+direction*scale;
    }
}

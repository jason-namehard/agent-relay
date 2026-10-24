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
        string Name(string id)=>displayName is null?id:displayName(id);
        Brush B(string key)=>(Brush)owner.FindResource(key);
        Brush Color(ConnectionView edge)=>edge.Display switch
        {
            "Allowed"=>B("AllowedBrush"),"Denied"=>B("DeniedBrush"),"Unavailable"=>B("UnavailableBrush"),
            _=>throw new ArgumentOutOfRangeException(nameof(edges),edge.Display,"Unknown connection display state.")
        };
        Stretch=Stretch.Uniform;StretchDirection=StretchDirection.DownOnly;HorizontalAlignment=HorizontalAlignment.Left;Margin=new(0,0,0,12);
        AutomationProperties.SetAutomationId(this,"ConnectionGraph");
        var body=new StackPanel{Width=820};Child=body;
        var canvas=new Canvas{Width=820,Height=380,Background=B("SurfaceBrush")};body.Children.Add(canvas);
        var ordered=agents.OrderBy(a=>a.AgentId,StringComparer.Ordinal).ToArray();
        var directions=edges.ToDictionary(e=>(e.From,e.To));
        var positions=ordered.Select((a,i)=>(a.AgentId,Point:new Point(410+310*Math.Cos(-Math.PI/2+2*Math.PI*i/ordered.Length),190+145*Math.Sin(-Math.PI/2+2*Math.PI*i/ordered.Length))))
            .ToDictionary(p=>p.AgentId,p=>p.Point);
        if(ordered.Length==0)canvas.Children.Add(new TextBlock{Text=UiText.Translate("Register agents to view directed connections.",language),Margin=new(20)});
        for(var i=0;i<ordered.Length;i++)
        for(var j=i+1;j<ordered.Length;j++)
        {
            var from=ordered[i].AgentId;var to=ordered[j].AgentId;
            var forward=directions[(from,to)];var reverse=directions[(to,from)];
            var hasForward=forward.Display!="Unavailable";var hasReverse=reverse.Display!="Unavailable";
            var a=positions[from];var b=positions[to];var direction=b-a;direction.Normalize();
            var start=NodeBoundary(a,b-a);var end=NodeBoundary(b,a-b);
            var midpoint=new Point((start.X+end.X)/2,(start.Y+end.Y)/2);
            var tooltip=$"{Name(from)} → {Name(to)}: {GraphText.State(forward.Display,language)}\n{Name(to)} → {Name(from)}: {GraphText.State(reverse.Display,language)}";
            var hit=new System.Windows.Shapes.Path{Data=new LineGeometry(start,end),Stroke=Brushes.Transparent,StrokeThickness=18,
                Cursor=Cursors.Hand,ToolTip=tooltip,Tag=forward,Focusable=true};
            AutomationProperties.SetAutomationId(hit,$"ConnectionPair:{from}:{to}");
            AutomationProperties.SetName(hit,tooltip);
            hit.MouseLeftButtonDown+=(_,e)=>{select(forward);e.Handled=true;};
            hit.KeyDown+=(_,e)=>{if(e.Key is Key.Enter or Key.Space){select(forward);e.Handled=true;}};
            canvas.Children.Add(hit);
            void Stroke(Point first,Point last,Brush color,string id,bool dashed=false)
            {
                var line=new System.Windows.Shapes.Path{Data=new LineGeometry(first,last),Stroke=color,StrokeThickness=2.5,IsHitTestVisible=false};
                if(dashed)line.StrokeDashArray=new DoubleCollection([4,4]);
                AutomationProperties.SetAutomationId(line,id);canvas.Children.Add(line);
            }
            if(hasForward&&hasReverse)
            {
                Stroke(start,midpoint,Color(reverse),$"ConnectionStroke:{to}:{from}");
                Stroke(midpoint,end,Color(forward),$"ConnectionStroke:{from}:{to}");
            }
            else if(hasForward)Stroke(start,end,Color(forward),$"ConnectionStroke:{from}:{to}");
            else if(hasReverse)Stroke(start,end,Color(reverse),$"ConnectionStroke:{to}:{from}");
            else Stroke(start,end,B("UnavailableBrush"),$"ConnectionStroke:{from}:{to}",true);
            if(hasForward)AddArrow(canvas,end,direction,Color(forward),$"ConnectionArrow:{from}:{to}");
            if(hasReverse)AddArrow(canvas,start,-direction,Color(reverse),$"ConnectionArrow:{to}:{from}");
        }
        foreach(var agent in ordered)
        {
            var p=positions[agent.AgentId];var name=Name(agent.AgentId);
            var node=new Border{Width=150,MinHeight=42,Padding=new(8,7,8,7),Background=B("SurfaceAltBrush"),
                BorderBrush=B("BorderBrush"),BorderThickness=new(1),CornerRadius=new(12),ToolTip=$"{name}\n{agent.AgentId}"};
            AutomationProperties.SetAutomationId(node,$"ConnectionNode:{agent.AgentId}");
            node.Child=new TextBlock{Text=name,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,Foreground=B("TextBrush")};
            Canvas.SetLeft(node,p.X-75);Canvas.SetTop(node,p.Y-21);Panel.SetZIndex(node,2);canvas.Children.Add(node);
        }
        var legend=new WrapPanel{Margin=new(12,12,12,4)};
        AutomationProperties.SetAutomationId(legend,"ConnectionLegend");body.Children.Add(legend);
        foreach(var kind in new[]{"single","double","none"})
        {
            var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,0,22,10)};
            AutomationProperties.SetAutomationId(row,"ConnectionLegend"+char.ToUpperInvariant(kind[0])+kind[1..]);
            var sample=new Canvas{Width=68,Height=24,Margin=new(0,0,8,0)};
            var color=kind=="none"?B("UnavailableBrush"):B("AllowedBrush");
            var line=new Line{X1=8,Y1=12,X2=60,Y2=12,Stroke=color,StrokeThickness=2.5};
            if(kind=="none")line.StrokeDashArray=new DoubleCollection([4,4]);sample.Children.Add(line);
            if(kind!="none")AddArrow(sample,new Point(60,12),new Vector(1,0),color);
            if(kind=="double")AddArrow(sample,new Point(8,12),new Vector(-1,0),color);
            row.Children.Add(sample);row.Children.Add(new TextBlock{Text=GraphText.Legend(kind,language),VerticalAlignment=VerticalAlignment.Center,Foreground=B("TextBrush")});
            legend.Children.Add(row);
        }
        body.Children.Add(new TextBlock{Text=GraphText.Legend("colors",language),TextWrapping=TextWrapping.Wrap,Foreground=B("MutedBrush"),Margin=new(12,0,12,10)});
    }

    private static void AddArrow(Canvas canvas,Point tip,Vector direction,Brush color,string? id=null)
    {
        var side=new Vector(-direction.Y,direction.X);
        var arrow=new Polygon{Points=new PointCollection([tip,tip-direction*11+side*4.5,tip-direction*11-side*4.5]),Fill=color,IsHitTestVisible=false};
        if(id is not null)AutomationProperties.SetAutomationId(arrow,id);
        canvas.Children.Add(arrow);
    }

    private static Point NodeBoundary(Point center,Vector direction)
    {
        var scale=Math.Min(81/Math.Abs(direction.X),27/Math.Abs(direction.Y));
        return center+direction*scale;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AgentRelay.Desktop;

internal static class NavigationIcon
{
    public static Border Create(string page,Brush stroke)
    {
        var shape=page switch
        {
            "Home"=>"M3,11 L12,3 L21,11 M6,9 L6,21 L10,21 L10,15 L14,15 L14,21 L18,21 L18,9",
            "Agents"=>"M16,7 A4,4 0 1 1 8,7 A4,4 0 1 1 16,7 M4,21 L4,18 Q4,13 12,13 Q20,13 20,18 L20,21",
            "Connections"=>"M4,4 L9,4 L9,9 L4,9 Z M15,15 L20,15 L20,20 L15,20 Z M9,6 L17,6 L17,12 M14,9 L17,12 L20,9 M15,18 L7,18 L7,12 M4,15 L7,12 L10,15",
            "Inbox"=>"M3,5 L21,5 L21,20 L3,20 Z M3,13 L8,13 L10,16 L14,16 L16,13 L21,13",
            "Adapters"=>"M8,2 L8,7 M16,2 L16,7 M5,7 L19,7 L19,11 Q19,16 12,16 Q5,16 5,11 Z M12,16 L12,19 Q12,22 16,22",
            "Audit"=>"M5,3 L15,3 L20,8 L20,21 L5,21 Z M15,3 L15,8 L20,8 M8,12 L16,12 M8,16 L14,16",
            "Settings"=>"M9,2 L15,2 L15.6,5 L18,6.4 L20.8,5.4 L23.8,10.6 L21.5,12.6 L21.5,15.4 L23.8,17.4 L20.8,22.6 L18,21.6 L15.6,23 L15,26 L9,26 L8.4,23 L6,21.6 L3.2,22.6 L0.2,17.4 L2.5,15.4 L2.5,12.6 L0.2,10.6 L3.2,5.4 L6,6.4 L8.4,5 Z M17,14 A5,5 0 1 1 7,14 A5,5 0 1 1 17,14",
            _=>throw new InvalidOperationException(page)
        };
        var path=new Path{Data=Geometry.Parse(shape),Stroke=stroke,StrokeThickness=1.5,StrokeLineJoin=PenLineJoin.Round,
            StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Width=20,Height=20,Stretch=Stretch.Uniform};
        return new Border{Child=path,Width=20,Height=20,VerticalAlignment=VerticalAlignment.Center};
    }
}

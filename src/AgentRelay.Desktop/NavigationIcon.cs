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
            "Agents"=>"M12,3 A4,4 0 1 1 11.99,3 M4,21 L4,18 Q4,13 12,13 Q20,13 20,18 L20,21 Z",
            "Connections"=>"M4,4 L9,4 L9,9 L4,9 Z M15,15 L20,15 L20,20 L15,20 Z M9,6 L17,6 L17,12 M14,9 L17,12 L20,9 M15,18 L7,18 L7,12 M4,15 L7,12 L10,15",
            "Inbox"=>"M3,5 L21,5 L21,20 L3,20 Z M3,13 L8,13 L10,16 L14,16 L16,13 L21,13",
            "Adapters"=>"M8,3 L8,8 M16,3 L16,8 M5,8 L19,8 L19,12 Q19,17 12,17 Q5,17 5,12 Z M12,17 L12,22",
            "Audit"=>"M5,3 L19,3 L19,21 L5,21 Z M8,8 L16,8 M8,12 L16,12 M8,16 L14,16",
            "Settings"=>"M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M8,3 L8,9 M16,9 L16,15 M10,15 L10,21",
            _=>throw new InvalidOperationException(page)
        };
        var path=new Path{Data=Geometry.Parse(shape),Stroke=stroke,StrokeThickness=1.6,StrokeLineJoin=PenLineJoin.Round,
            StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Width=22,Height=22,Stretch=Stretch.Uniform};
        return new Border{Child=path,Width=30,Height=30,Padding=new(4),CornerRadius=new(9),Background=new SolidColorBrush(Color.FromArgb(20,255,255,255))};
    }
}

using System.Windows;
using System.Windows.Controls;

namespace MapleHelper.App.Controls;

/// <summary>Trạng thái trống có một con quái Maple, dùng thay cho dòng chữ "không có dữ liệu".</summary>
public partial class EmptyState : UserControl
{
    public static readonly DependencyProperty SpriteProperty = DependencyProperty.Register(
        nameof(Sprite), typeof(string), typeof(EmptyState), new PropertyMetadata("mobs/slime"));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(EmptyState), new PropertyMetadata(""));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(EmptyState), new PropertyMetadata(""));

    public EmptyState()
    {
        InitializeComponent();
    }

    public string Sprite
    {
        get => (string)GetValue(SpriteProperty);
        set => SetValue(SpriteProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}

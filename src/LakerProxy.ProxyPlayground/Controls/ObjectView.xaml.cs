using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LakerProxy.ProxyPlayground.Controls
{
  /// <summary>
  /// Interaction logic for ObjectView.xaml
  /// </summary>
  public partial class ObjectView : UserControl
  {
    public ObjectView()
    {
      InitializeComponent();
    }

    private void UIElement_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
      var textBlock = sender as TextBlock;
      if (textBlock == null)
        return;

      Clipboard.SetText(textBlock.Text);
    }


  }
}

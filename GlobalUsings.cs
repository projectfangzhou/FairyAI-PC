// Global type aliases to resolve WPF/WinForms naming conflicts
// (UseWindowsForms=true adds implicit usings for System.Drawing and System.Windows.Forms)
global using Application = System.Windows.Application;
global using Color = System.Windows.Media.Color;
global using Brush = System.Windows.Media.Brush;
global using Rectangle = System.Windows.Shapes.Rectangle;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using RoutedEventArgs = System.Windows.RoutedEventArgs;

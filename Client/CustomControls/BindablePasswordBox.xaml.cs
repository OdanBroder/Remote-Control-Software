using System.Windows;
using System.Windows.Controls;

namespace Client.CustomControls
{
    public partial class BindablePasswordBox : UserControl
    {
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register(
                nameof(Password),
                typeof(string),
                typeof(BindablePasswordBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChangedExternally)
            );

        public string Password
        {
            get => (string)GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        public BindablePasswordBox()
        {
            InitializeComponent();
            txtPassword.PasswordChanged += TxtPassword_PasswordChanged;
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (Password != txtPassword.Password)
            {
                Password = txtPassword.Password;
            }
        }

        private static void OnPasswordChangedExternally(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (BindablePasswordBox)d;

            if (control.txtPassword.Password != (string)e.NewValue)
            {
                control.txtPassword.Password = (string)e.NewValue ?? string.Empty;
            }
        }
    }
}

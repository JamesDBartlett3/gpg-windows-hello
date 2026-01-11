using System.Windows.Forms;
using System.Drawing;

namespace GpgWindowsHello;

public class PassphraseInputDialog : Form
{
    private readonly TextBox _textBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public string? Passphrase { get; private set; }

    public PassphraseInputDialog(string title, string prompt)
    {
        Text = title;
        Size = new Size(500, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;

        var messageLabel = new Label
        {
            Text = prompt,
            Location = new Point(20, 20),
            Size = new Size(440, 130),
            TextAlign = ContentAlignment.TopLeft
        };

        _textBox = new TextBox
        {
            Location = new Point(20, 160),
            Size = new Size(440, 25),
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };

        _okButton = new Button
        {
            Text = "OK",
            Location = new Point(280, 220),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK
        };

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(380, 220),
            Size = new Size(80, 30),
            DialogResult = DialogResult.Cancel
        };

        _okButton.Click += (s, e) => { Passphrase = _textBox.Text; Close(); };
        _cancelButton.Click += (s, e) => { Passphrase = null; Close(); };
        
        Controls.AddRange(new Control[] { 
            messageLabel, _textBox, _okButton, _cancelButton 
        });
        
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }
}
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using InTheHand.Net;
using InTheHand.Net.Sockets;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SenderForm());
    }
}

internal sealed class SenderForm : Form
{
    private readonly TextBox address = new() { Width = 220, PlaceholderText = "Phone Bluetooth address" };
    private readonly TextBox file = new() { Width = 330, ReadOnly = true };
    private readonly Button browse = new() { Text = "Browse..." };
    private readonly Button send = new() { Text = "Send File", Width = 120, Height = 36 };
    private readonly ProgressBar progress = new() { Width = 440, Height = 24 };
    private readonly Label status = new() { AutoSize = true, Text = "Pair your phone with Windows, then start the receiver on the phone." };

    private static readonly Guid ServiceId =
        Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    public SenderForm()
    {
        Text = "APCI Bluetooth File Sender";
        Width = 560;
        Height = 290;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Phone address:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(address, 1, 0);
        layout.Controls.Add(new Label { Text = "File to send:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);

        var fileRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        fileRow.Controls.Add(file);
        fileRow.Controls.Add(browse);
        layout.Controls.Add(fileRow, 1, 1);

        layout.Controls.Add(new Label { Text = "Transfer:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(progress, 1, 2);
        layout.Controls.Add(new Label { Text = "Action:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(send, 1, 3);
        layout.Controls.Add(status, 0, 4);
        layout.SetColumnSpan(status, 2);
        Controls.Add(layout);

        browse.Click += (_, _) => PickFile();
        send.Click += async (_, _) => await SendFileAsync();
    }

    private void PickFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose any file to send",
            Filter = "All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            file.Text = dialog.FileName;
    }

    private async System.Threading.Tasks.Task SendFileAsync()
    {
        string selectedPath = file.Text;
        if (!File.Exists(selectedPath))
        {
            MessageBox.Show(this, "Click Browse and choose a file first.");
            return;
        }

        BluetoothAddress btAddress;
        try
        {
            btAddress = BluetoothAddress.Parse(address.Text.Trim());
        }
        catch
        {
            MessageBox.Show(this, "Enter the phone's Bluetooth address shown in the phone app.");
            return;
        }

        browse.Enabled = send.Enabled = false;
        progress.Value = 0;
        status.Text = "Connecting to phone...";
        try
        {
            await System.Threading.Tasks.Task.Run(() => Transfer(btAddress, selectedPath));
            progress.Value = 100;
            status.Text = "Transfer complete. Check the phone.";
        }
        catch (Exception ex)
        {
            status.Text = "Transfer failed.";
            MessageBox.Show(this, ex.Message, "APCI Bluetooth File Sender",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            browse.Enabled = send.Enabled = true;
        }
    }

    private void Transfer(BluetoothAddress btAddress, string filePath)
    {
        using var client = new BluetoothClient();
        var endpoint = new BluetoothEndPoint(btAddress, ServiceId);
        client.Connect(endpoint);

        using NetworkStream stream = client.GetStream();
        using var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        string headerText = Path.GetFileName(filePath) + "\n" + input.Length + "\n";
        byte[] header = Encoding.UTF8.GetBytes(headerText);
        stream.Write(header, 0, header.Length);

        byte[] buffer = new byte[16 * 1024];
        long sent = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            stream.Write(buffer, 0, read);
            sent += read;
            int percent = input.Length == 0 ? 100 : (int)(sent * 100 / input.Length);
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed)
                            progress.Value = Math.Max(0, Math.Min(100, percent));
                    }));
                }
                catch (InvalidOperationException) { }
            }
        }
        stream.Flush();
    }
}

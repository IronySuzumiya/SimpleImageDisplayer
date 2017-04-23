using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace SimpleImageDisplayer
{
    public partial class MainForm : Form
    {
        private static readonly int IMG_ROW = 50;
        private static readonly int IMG_COL = 225;
        private Bitmap image = new Bitmap(IMG_COL, IMG_ROW);
        private int bufferCol = 0;
        private int bufferRow = 0;

        private static readonly int ENSURE_CNT_MAX = 5;
        private static readonly byte FRAME_FLAG = 0xee;

        private int ensureCnt = 0;
        private bool inFrame = false;

        private bool serialPortIsOpen = false;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            serialPort.NewLine = "\r\n";
            btnRefresh.PerformClick();
            picImage.GetType().GetProperty("DoubleBuffered"
                , BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(picImage, true, new object[] { });
            picImage.Image = image;
        }

        private void picImage_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.ScaleTransform((float)picImage.Width / IMG_COL, (float)picImage.Height / IMG_ROW);
            e.Graphics.DrawImage(picImage.Image, 0, 0);
        }

        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(100);
            Invoke(new EventHandler((_, __) =>
            {
                var bytes = new byte[serialPort.BytesToRead];
                serialPort.Read(bytes, 0, bytes.Length);
                int i = 0;
                while (i < bytes.Length)
                {
                    if (!inFrame)
                    {
                        for (; i < bytes.Length; ++i)
                        {
                            if (bytes[i] == FRAME_FLAG)
                            {
                                ++ensureCnt;
                            }
                            else
                            {
                                ensureCnt = 0;
                            }
                            if (ensureCnt == ENSURE_CNT_MAX)
                            {
                                ++i;
                                ensureCnt = 0;
                                inFrame = true;
                                break;
                            }
                        }
                    }
                    if (inFrame)
                    {
                        for (; i < bytes.Length; ++i)
                        {
                            if (bufferCol >= IMG_COL)
                            {
                                bufferCol = 0;
                                ++bufferRow;
                            }
                            if (bufferRow >= IMG_ROW)
                            {
                                bufferRow = 0;
                                bufferCol = 0;
                                inFrame = false;
                                picImage.Refresh();
                                break;
                            }
                            for(int j = 0; j < 8 && bufferCol < IMG_COL; ++bufferCol, ++j)
                            {
                                image.SetPixel(bufferCol, bufferRow, (bytes[i] & (0x01 << j)) == 0x00 ? Color.White : Color.Black);
                            }
                        }
                    }
                }
            }));
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            foreach (var port in SerialPort.GetPortNames())
            {
                cmbPortName.Items.Add(port);
            }
            if (cmbPortName.Items.Count != 0)
            {
                cmbPortName.SelectedIndex = 0;
            }
        }

        private void btnOpenPort_Click(object sender, EventArgs e)
        {
            try
            {
                if (!serialPortIsOpen)
                {
                    serialPort.PortName = cmbPortName.Text;
                    serialPort.Open();
                    serialPortIsOpen = true;
                    btnRefresh.Enabled = false;
                    btnOpenImage.Enabled = false;
                    btnOpenPort.Text = "关闭串口";
                }
                else
                {
                    serialPort.Close();
                    serialPortIsOpen = false;
                    btnRefresh.Enabled = true;
                    btnOpenImage.Enabled = true;
                    btnOpenPort.Text = "打开串口";
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (serialPortIsOpen)
                {
                    serialPort.Close();
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnOpenImage_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    image = (Bitmap)Image.FromFile(openFileDialog.FileName);
                    picImage.Image = image;
                    picImage.Refresh();
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void btnOpenData_Click(object sender, EventArgs e)
        {
            if(openFileDialog.ShowDialog() == DialogResult.OK)
            {
                using (var file = File.OpenRead(openFileDialog.FileName))
                {
                    using (var reader = new StreamReader(file))
                    {
                        try
                        {
                            image = new Bitmap(IMG_COL, IMG_ROW);
                            var content = reader.ReadToEnd();
                            var hexStringArray = content.Trim().Split(' ', '\n');
                            byte value;
                            for (int i = 0; i < hexStringArray.Length; ++i)
                            {
                                value = byte.Parse(hexStringArray[i], System.Globalization.NumberStyles.HexNumber);
                                image.SetPixel(i % IMG_COL, i / IMG_COL, value != 0x00 ? Color.White : Color.Black);
                            }
                            picImage.Image = image;
                            picImage.Refresh();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }

        private void btnSaveImage_Click(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    image.Save(saveFileDialog.FileName, ImageFormat.Bmp);
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void btnSaveData_Click(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                using (var file = File.OpenWrite(saveFileDialog.FileName))
                {
                    using (var writer = new StreamWriter(file))
                    {
                        try
                        {
                            int cnt = 0;
                            for (int row = 0; row < IMG_ROW; ++row)
                            {
                                for (int col = 0; col < IMG_COL; ++col)
                                {
                                    ++cnt;
                                    writer.Write(image.GetPixel(col, row) == Color.FromArgb(unchecked((int)0xffffffff)) ? "FE" : "00");
                                    writer.Write(cnt % 16 == 0 ? "\n" : " ");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }

        private void picImage_SizeChanged(object sender, EventArgs e)
        {
            picImage.Refresh();
        }
    }
}

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
        private byte[,] imageBuffer = new byte[IMG_ROW, IMG_COL / 8 + 1];
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

        unsafe private void RenderImage(byte[,] imageBuffer)
        {
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height)
                , ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            for (int row = 0; row < data.Height; ++row)
            {
                for (int col = 0; col < data.Width; ++col)
                {
                    var color = (byte*)data.Scan0 + row * data.Stride + col * 3;
                    if ((imageBuffer[row, col / 8] & (0x01 << (col % 8))) != 0)
                    {
                        color[0] = 255;
                        color[1] = 255;
                        color[2] = 255;
                    }
                    else
                    {
                        color[0] = 0;
                        color[1] = 0;
                        color[2] = 0;
                    }
                }
            }
            image.UnlockBits(data);
            picImage.Refresh();
        }

        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(100);
            Invoke(new EventHandler(delegate
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
                    btnOpenData.Enabled = false;
                    btnOpenPort.Text = "关闭串口";
                }
                else
                {
                    serialPort.Close();
                    serialPortIsOpen = false;
                    btnRefresh.Enabled = true;
                    btnOpenData.Enabled = true;
                    btnOpenPort.Text = "打开串口";
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnOpenData_Click(object sender, EventArgs e)
        {
            openFileDialog.ShowDialog();
        }

        private void openFileDialog_FileOk(object sender, CancelEventArgs e)
        {
            using (var file = File.OpenRead(openFileDialog.FileName))
            {
                using (var reader = new StreamReader(file))
                {
                    var content = reader.ReadToEnd();
                    var hexStringArray = content.Trim().Split(' ', '\n');
                    byte value;
                    for(int i = 0; i < hexStringArray.Length; ++i)
                    {
                        value = byte.Parse(hexStringArray[i], System.Globalization.NumberStyles.HexNumber);
                        if (value != 0x00)
                        {
                            imageBuffer[i / IMG_COL, (i % IMG_COL) / 8] |= (byte)(0x01 << (i % IMG_COL % 8));
                        }
                        else
                        {
                            imageBuffer[i / IMG_COL, (i % IMG_COL) / 8] &= (byte)~(0x01 << (i % IMG_COL % 8));
                        }
                    }
                    RenderImage(imageBuffer);
                }
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
    }
}

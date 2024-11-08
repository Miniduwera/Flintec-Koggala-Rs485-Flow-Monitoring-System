using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_New
{
    public partial class Form1 : Form
    {
        public int statVal;
        public byte functionCode = 4;
        ushort startingAddress = 0x1010; // 4112 in decimal
        ushort quantityOfRegisters = 20;
        int slave_ID_N = 1;
        String Last_Read_Time_String;
        String CurrentTimeString;
        int j;
        public Form1()
        {
            InitializeComponent();
            backgroundWorker1.WorkerReportsProgress = true;
            backgroundWorker1.WorkerSupportsCancellation = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            cmBoxComPort.Items.AddRange(ports);
        }
        public string GetCurrentDateTime()
        {
            DateTime now = DateTime.Now;
            return now.ToString("yyyy-MM-dd HH:mm:ss"); // Adjust the format string as per your requirement
        }
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.PortName = cmBoxComPort.Text;
                serialPort1.BaudRate = Convert.ToInt32(cmBoxBaudRate.Text);
                serialPort1.DataBits = Convert.ToInt32(cmBoxDataBits.Text);
                serialPort1.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cmBoxStopBits.Text);
                serialPort1.Parity = (Parity)Enum.Parse(typeof(Parity), cmBoxParityBits.Text);

                serialPort1.Open();
                progressBar1.Value = 100;
                Com_Stat.Text = "ON";

            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDissConnect_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
                progressBar1.Value = 0;
                Com_Stat.Text = "OFF";
            }
        }

        public static byte[] ReverseBytes(byte[] bytes)
        {
            Array.Reverse(bytes);
            return bytes;
        }
        public static uint ToUInt32BigEndian(byte[] dataBytes, int startIndex)
        {
            byte[] temp = new byte[4];
            Array.Copy(dataBytes, startIndex, temp, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                temp = ReverseBytes(temp);
            }
            return BitConverter.ToUInt32(temp, 0);
        }

        public static float ToSingleBigEndian(byte[] dataBytes, int startIndex)
        {
            byte[] temp = new byte[4];
            Array.Copy(dataBytes, startIndex, temp, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                temp = ReverseBytes(temp);
            }
            return BitConverter.ToSingle(temp, 0);
        }
        static ushort CalculateCRC(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int pos = 0; pos < length; pos++)
            {
                crc ^= data[pos];
                for (int i = 8; i != 0; i--)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;

        }
        public byte[] construct_request(byte slaveId)
        {
            byte[] request = new byte[8];
            request[0] = (byte)slaveId;
            request[1] = functionCode;
            request[2] = (byte)(startingAddress >> 8);
            request[3] = (byte)(startingAddress & 0xFF);
            request[4] = (byte)(quantityOfRegisters >> 8);
            request[5] = (byte)(quantityOfRegisters & 0xFF);
            ushort crc = CalculateCRC(request, 6);
            request[6] = (byte)(crc & 0xFF);
            request[7] = (byte)(crc >> 8);
            return request;
        }
        public float[] readMeterData(byte slaveID)
        {
            byte[] request = construct_request(slaveID);


            try
            {
                serialPort1.Write(request, 0, request.Length);
                byte[] response = new byte[5 + 2 * quantityOfRegisters];
                serialPort1.ReadTimeout = 100;
                int bytesRead = serialPort1.Read(response, 0, response.Length);

                if (bytesRead >= 5)
                {
                    byte slaveIdResp = response[0];
                    byte functionCodeResp = response[1];
                    byte byteCount = response[2];

                    if (slaveIdResp == slaveID && functionCodeResp == functionCode)
                    {
                        byte[] dataBytes = new byte[byteCount];
                        Array.Copy(response, 3, dataBytes, 0, byteCount);
                        ushort crcResp = BitConverter.ToUInt16(response, 3 + byteCount);
                        ushort crcCalculated = CalculateCRC(response, 3 + byteCount);

                        if (crcResp == crcCalculated)
                        {
                            float address_4112 = ToSingleBigEndian(dataBytes, 0);
                            float address_4114 = ToSingleBigEndian(dataBytes, 4);
                            float address_4116 = ToSingleBigEndian(dataBytes, 8);
                            float address_4118 = ToSingleBigEndian(dataBytes, 12);
                            uint address_4120 = ToUInt32BigEndian(dataBytes, 16);
                            float address_4122 = ToSingleBigEndian(dataBytes, 20);
                            uint address_4124 = ToUInt32BigEndian(dataBytes, 24);
                            float address_4126 = ToSingleBigEndian(dataBytes, 28);

                            float totalCommulativePositiveVal = address_4120 + address_4122;
                            float totalCommulativeNegativeVal = address_4124 + address_4126;
                            

                            float[] floatValues = { address_4112, address_4114, address_4116, address_4118, totalCommulativePositiveVal, totalCommulativeNegativeVal };
                            Console.WriteLine("NIce!!");
                            Console.WriteLine("NIce!!");
                            Console.WriteLine("NIce!!");
                            statVal = 1;
                            return floatValues;

                        }
                        else
                        {
                            // Log the error and return null
                            Console.WriteLine("Invalid CRC in response.");
                            if (j>1)
                            {
                                statVal = 2;
                                backgroundWorker1.ReportProgress(0, "Invalid CRC in Response");
                            }
                           
                            return null;
                        }
                    }
                    else
                    {
                        // Log the error and return null
                        Console.WriteLine("Invalid response: mismatched slave ID or function code.");
                        if (j > 1)
                        {
                            statVal = 3;
                            backgroundWorker1.ReportProgress(0, "Invalid response: mismatched slave ID or function code.");
                        }
                        return null;
                    }
                }
                else
                {
                    // Log the error and return null
                    Console.WriteLine("Invalid response length.");
                    if (j > 1)
                    {
                        statVal = 4;
                        backgroundWorker1.ReportProgress(0, "Invalid response length.");
                    }
                    return null;
                }
            }
            catch (TimeoutException)
            {
                // Log the timeout and return null
                Console.WriteLine("Timeout: No data received");
                if (j > 1)
                {
                    statVal = 5;
                    backgroundWorker1.ReportProgress(0, "Timeout: No data received");
                }
                return null;
            }
            catch (Exception ex)
            {
                // Log any other exceptions and return null
                Console.WriteLine("An error occurred: " + ex.Message);
                if (j > 1)
                {
                    statVal = 6;
                    backgroundWorker1.ReportProgress(0, "Timeout: No data received");
                }
                return null;
            }
        }
    
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {

            float[] finalval = null;
            while (!backgroundWorker1.CancellationPending)
            {
                
                if (serialPort1.IsOpen)
                {
                    for (byte i = 1; i <= 15; i++)
                    {
                        for (j = 0; j < 3; j++)
                        {
                            Thread.Sleep(100);
                            finalval = readMeterData(i);
                            slave_ID_N = (int)(i);
                            if (finalval != null)
                            {
                                backgroundWorker1.ReportProgress(0, finalval);
                            }
                            //if (statVal == 1) 
                            //{
                            //    break;
                            //}
                        }
                    }
                }
            }
            e.Result = finalval;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                label7.Text = "operation Cancelled";
            }
            else if (e.Error != null)
            {
                label7.Text = e.Error.Message;
            }
            else
            {
                // label6.Text = e.Result.ToString();
                Console.WriteLine(e.Result.ToString());
            }
        }

        private void btnStartProcessing_Click(object sender, EventArgs e)
        {
            if (!backgroundWorker1.IsBusy)
            {
                backgroundWorker1.RunWorkerAsync();
            }
        }

        private void btnStopProcessing_Click(object sender, EventArgs e)
        {
            if (backgroundWorker1.IsBusy)
            {
                backgroundWorker1.CancelAsync();
                while (backgroundWorker1.CancellationPending)
                {
                    Application.DoEvents();
                }
            }
            if (serialPort1 != null && serialPort1.IsOpen) 
            {
                serialPort1.Close();
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is float[] floatValues)
            {
                // Convert the float values to a string for display
                //string displayText = string.Join(", ", floatValues.Select(f => f.ToString("F2")));

                string valInst_Flow = floatValues[0].ToString();
                string valInst_velo = floatValues[1].ToString();
                string valflow_percent = floatValues[2].ToString();
                string val_Conduct = floatValues[3].ToString();
                string val_T_C_P = floatValues[4].ToString();
                string val_T_C_N = floatValues[5].ToString();

                string txtInst_Flow_ = "txtInst_Flow_" + slave_ID_N;
                TextBox txtIns_Flow = this.Controls.Find(txtInst_Flow_, true).FirstOrDefault() as TextBox;
                txtIns_Flow.Text = valInst_Flow;


                string txtInst_velo_ = "txtInst_velo_" + slave_ID_N;
                TextBox txtIns_velo = this.Controls.Find(txtInst_velo_, true).FirstOrDefault() as TextBox;
                txtIns_velo.Text = valInst_velo;

                string txtflow_percent_ = "txtflow_percent_" + slave_ID_N;
                TextBox txtflo_percent = this.Controls.Find(txtflow_percent_, true).FirstOrDefault() as TextBox;
                txtflo_percent.Text = valflow_percent;

                string txt_Conduct_ = "txt_Conduct_" + slave_ID_N;
                TextBox txt_Conduct = this.Controls.Find(txt_Conduct_, true).FirstOrDefault() as TextBox;
                txt_Conduct.Text = val_Conduct;

                string txt_T_C_P_ = "txt_T_C_P_" + slave_ID_N;
                TextBox txt_T_C_P = this.Controls.Find(txt_T_C_P_, true).FirstOrDefault() as TextBox;
                txt_T_C_P.Text = val_T_C_P;

                string txt_T_C_N_ = "txt_T_C_N_" + slave_ID_N;
                TextBox txt_T_C_N = this.Controls.Find(txt_T_C_N_, true).FirstOrDefault() as TextBox;
                txt_T_C_N.Text = val_T_C_N;

                string txt_T_C_V_ = "txt_T_C_V_" + slave_ID_N;
                TextBox txt_T_C_V = this.Controls.Find(txt_T_C_V_, true).FirstOrDefault() as TextBox;
                txt_T_C_V.Text = val_T_C_P;

                string errlblStat_ = "lblStat_"+ slave_ID_N;
                Label errlblStat = this.Controls.Find(errlblStat_, true).FirstOrDefault() as Label;
                errlblStat.ForeColor = Color.LimeGreen;
                errlblStat.Text = "ONLINE";

                //lbl_LRT_1
                string lbl_LRT_ = "lbl_LRT_" + slave_ID_N;
                Label lbl_LRT = this.Controls.Find(lbl_LRT_, true).FirstOrDefault() as Label;
                Last_Read_Time_String = GetCurrentDateTime();
                lbl_LRT.Text = Last_Read_Time_String;

                string pnl_indicator_ = "pnl_indicator_" + slave_ID_N;
                Panel pnl_indicator = this.Controls.Find(pnl_indicator_, true).FirstOrDefault() as Panel;
                pnl_indicator.BackColor = Color.LimeGreen;
            }
            
            if (e.UserState is string statstring )
            {
                if (statVal == 2) 
                {
                    string errlblStat_ = "lblStat_" + slave_ID_N;
                    Label errlblStat = this.Controls.Find(errlblStat_, true).FirstOrDefault() as Label;
                    errlblStat.ForeColor = Color.Red;
                    errlblStat.Text = "ERR_Ox1001";

                    string pnl_indicator_ = "pnl_indicator_" + slave_ID_N;
                    Panel pnl_indicator = this.Controls.Find(pnl_indicator_, true).FirstOrDefault() as Panel;
                    pnl_indicator.BackColor = Color.OrangeRed;
                }
                else if (statVal == 3)
                {
                    string errlblStat_ = "lblStat_" + slave_ID_N;
                    Label errlblStat = this.Controls.Find(errlblStat_, true).FirstOrDefault() as Label;
                    errlblStat.ForeColor = Color.Red;
                    errlblStat.Text = "ERR_0x1002";

                    string pnl_indicator_ = "pnl_indicator_" + slave_ID_N;
                    Panel pnl_indicator = this.Controls.Find(pnl_indicator_, true).FirstOrDefault() as Panel;
                    pnl_indicator.BackColor = Color.OrangeRed;
                }
                else if (statVal == 4)
                {
                    string errlblStat_ = "lblStat_" + slave_ID_N;
                    Label errlblStat = this.Controls.Find(errlblStat_, true).FirstOrDefault() as Label;
                    errlblStat.ForeColor = Color.Red;
                    errlblStat.Text = "ERR_0x1003";

                    string pnl_indicator_ = "pnl_indicator_" + slave_ID_N;
                    Panel pnl_indicator = this.Controls.Find(pnl_indicator_, true).FirstOrDefault() as Panel;
                    pnl_indicator.BackColor = Color.OrangeRed;
                }
                else if (statVal == 5)
                {
                    string errlblStat_ = "lblStat_" + slave_ID_N;
                    Label errlblStat = this.Controls.Find(errlblStat_, true).FirstOrDefault() as Label;
                    errlblStat.ForeColor = Color.Red;
                    errlblStat.Text = "ERR_0x2001";

                    string pnl_indicator_ = "pnl_indicator_" + slave_ID_N;
                    Panel pnl_indicator = this.Controls.Find(pnl_indicator_, true).FirstOrDefault() as Panel;
                    pnl_indicator.BackColor = Color.OrangeRed;
                }
                else if (statVal == 6)
                {
                    string errlblStat_ = "lblStat_" + slave_ID_N;
                    Label errlblStat = this.Controls.Find(errlblStat_, true).FirstOrDefault() as Label;
                    errlblStat.ForeColor = Color.Red;
                    errlblStat.Text = "ERR_0x2002";

                    string pnl_indicator_ = "pnl_indicator_" + slave_ID_N;
                    Panel pnl_indicator = this.Controls.Find(pnl_indicator_, true).FirstOrDefault() as Panel;
                    pnl_indicator.BackColor = Color.OrangeRed;
                }
                
            }
            Salve_on.Text = slave_ID_N.ToString();
            statVal = 1;
            CurrentTimeString = GetCurrentDateTime();
            lbl_Current_Time.Text = CurrentTimeString;


        }
    }
}

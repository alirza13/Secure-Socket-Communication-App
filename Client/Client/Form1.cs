using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Client
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool connected = false;
        Socket clientSocket;
        string RSAEncDec3072;
        string RSASignVer3072;
        bool enrolled = false;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();

            using (System.IO.StreamReader fileReader =   // Reading enc dec keys
            new System.IO.StreamReader("server_enc_dec_pub.txt"))
            {
                RSAEncDec3072 = fileReader.ReadLine();
            }

            using (System.IO.StreamReader fileReader =
        new System.IO.StreamReader("server_signing_verification_pub.txt"))       // Reading sign ver keys
            {
                RSASignVer3072 = fileReader.ReadLine();
            }

            EnrollButton.Enabled = false;
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string IP = ipAddress.Text;
            int port;
            if (Int32.TryParse(portNum.Text, out port))
            {
                try
                {
                    clientSocket.Connect(IP, port);
                    connectButton.Enabled = false;
                    connected = true;
                    EnrollButton.Enabled = true;     // Being enabled after connection
                    logs.AppendText("Connected to server\n");


                    Thread receiveThread = new Thread(new ThreadStart(Receive));
                    receiveThread.Start();

                }
                catch
                {
                    logs.AppendText("Could not connect to server\n");
                }
            }
            else
            {
                logs.AppendText("Check the portnumber\n");
            }
        }

        private void Receive()
        {
            while (connected)
            {
                try
                {
                    Byte[] buffer = new Byte[384]; //384
                    clientSocket.Receive(buffer);
                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));

                    if (incomingMessage.Contains("RandomNumber"))   // Receiving randomly generated number from server side
                    {
                        string rndString = "RandomNumber";
                        string rndNumber = incomingMessage.Substring(0, incomingMessage.IndexOf(rndString));
                        string password = passwordText.Text;
                        // hash using SHA-256
                        byte[] sha256 = hashWithSHA256(password);
                        byte[] upMostByte = new byte[16];
                        int counter = 0;

                        for (int i = 16; i < 32; i++)
                        {
                            upMostByte[counter] = sha256[i];
                            counter++;
                        }

                        // HMAC with SHA-256
                        byte[] hmacsha256 = applyHMACwithSHA256(rndNumber, upMostByte);
                        string hmac = Encoding.Default.GetString(hmacsha256);
                        string concat = String.Concat(hmac, "hmacRNDFromClient");

                        byte[] resultHMAC = Encoding.Default.GetBytes(concat);
                        clientSocket.Send(resultHMAC);

                    }
                    string msgAuth = " ", symEncDec = " " ;
                    string strSub = "successVerificationAuthentication";
                    string signature = " ", restForChecking = " ";
                    if (incomingMessage.Contains("successVerificationAuthentication"))
                    {
                        symEncDec = incomingMessage.Substring(strSub.Length - 1, 24);
                        msgAuth   = incomingMessage.Substring(strSub.Length + symEncDec.Length - 1, 24);
                        signature = incomingMessage.Substring(0, incomingMessage.IndexOf(strSub));
                        restForChecking = incomingMessage.Substring(incomingMessage.IndexOf(strSub));
                    }
                    
                    bool verifiedErrorEnroll = verifyWithRSA("errorVerificationEnrollment", 3072, RSASignVer3072, buffer);
                    bool verifiedSuccesEnroll = verifyWithRSA("successVerificationEnrollment", 3072, RSASignVer3072, buffer);

                    string toBeVerifiedForAuth = String.Concat(strSub, symEncDec);
                    string toBeVerifiedForAuthResult = String.Concat(toBeVerifiedForAuth, msgAuth);


                    bool verifiedErrorAuth = verifyWithRSA("errorVerificationAuthentication", 3072, RSASignVer3072, buffer);
                    bool verifiedSuccessAuth = verifyWithRSA(restForChecking, 3072, RSASignVer3072, Encoding.Default.GetBytes(signature));

                    

                    if (verifiedErrorEnroll)
                    {
                        logs.AppendText(Environment.NewLine + "Error.Try with another username");
                    }
                    else if (verifiedSuccesEnroll)
                    {
                        enrolled = true;
                        logs.AppendText(Environment.NewLine + "Successfully enrolled");
                    }

                    if (verifiedErrorAuth)
                    {
                        logs.AppendText(Environment.NewLine + "Error.Could not authenticate");
                    }
                    else if (verifiedSuccessAuth)
                    {
                        logs.AppendText(Environment.NewLine + "Succesfully authenticated");
                    }

                }
                catch
                {
                    if (!terminating)
                    {
                        logs.AppendText("The server has disconnected\n");
                    }

                    clientSocket.Close();
                    connected = false;
                }
            }
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            connected = false;
            terminating = true;
            Environment.Exit(0);
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            String message = messageText.Text;

            if (message != "" && message.Length < 63)
            {
                Byte[] buffer = new Byte[64];
                buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
            }
        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        // hash function: SHA-256
        static byte[] hashWithSHA256(string input)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create a hasher object from System.Security.Cryptography
            SHA256CryptoServiceProvider sha256Hasher = new SHA256CryptoServiceProvider();
            // hash and save the resulting byte array
            byte[] result = sha256Hasher.ComputeHash(byteInput);

            return result;
        }

        // RSA encryption with varying bit length
        static byte[] encryptWithRSA(string input, int algoLength, string xmlStringKey)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlStringKey);
            byte[] result = null;

            try
            {
                //true flag is set to perform direct RSA encryption using OAEP padding
                result = rsaObject.Encrypt(byteInput, true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }

        private void authenticateButton_Click(object sender, EventArgs e)
        {
            string userName = userNameText.Text;
            string strConcat = String.Concat(userName, "AuthenticationRequest");
            byte[] authenByte = Encoding.Default.GetBytes(strConcat);
            clientSocket.Send(authenByte);
        }

        private void EnrollButton_Click(object sender, EventArgs e)
        {
            string userName = userNameText.Text;
            if (connected)
            {

                string password = passwordText.Text;
                // hash using SHA-256
                byte[] sha256 = hashWithSHA256(password);
                byte[] upMostByte = new byte[16];
                int counter = 0;
                for (int i = 16; i < 32; i++)
                {
                    upMostByte[counter] = sha256[i];
                    counter++;
                }
                string upMostString = generateHexStringFromByteArray(upMostByte);
                string strConcat = String.Concat(userName, upMostString);
                string strConcat1 = String.Concat(strConcat, "Sending enrollment to server");
                string strConcat2 = String.Concat(strConcat1, userName.Length);
                byte[] encryptedData = encryptWithRSA(strConcat2, 3072, RSAEncDec3072);

                clientSocket.Send(encryptedData);
            }
        }

        // helper functions
        static string generateHexStringFromByteArray(byte[] input)
        {
            string hexString = BitConverter.ToString(input);
            return hexString.Replace("-", "");
        }

        static byte[] hexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        // RSA decryption with varying bit length
        static byte[] decryptWithRSA(string input, int algoLength, string xmlStringKey)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlStringKey);
            byte[] result = null;

            try
            {
                result = rsaObject.Decrypt(byteInput, true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }

        // verifying with RSA
        static bool verifyWithRSA(string input, int algoLength, string xmlString, byte[] signature)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlString);
            bool result = false;

            try
            {
                result = rsaObject.VerifyData(byteInput, "SHA256", signature);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }

        // HMAC with SHA-256
        static byte[] applyHMACwithSHA256(string input, byte[] key)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create HMAC applier object from System.Security.Cryptography
            HMACSHA256 hmacSHA256 = new HMACSHA256(key);
            // get the result of HMAC operation
            byte[] result = hmacSHA256.ComputeHash(byteInput);

            return result;
        }
    }
}

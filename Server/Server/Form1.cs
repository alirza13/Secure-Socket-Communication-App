using System;
using System.Collections.Generic;
using System.IO;
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

namespace Server
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool listening = false;
        string RSAencrypted, RSAsign;
        string resultRSA, resultSignRSA;

        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        List<Socket> socketList = new List<Socket>();
        List<User> userList = new List<User>();
        uint randomInteger;
        string authentReqUserName;
        User authReqUser;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();


            using (System.IO.StreamReader fileReader =              // Keys for encry and decry
new System.IO.StreamReader("encrypted_server_enc_dec_pub_prv.txt"))
            {
                RSAencrypted = fileReader.ReadLine();
            }

            using (System.IO.StreamReader fileReader =                      // Keys for verification and signing
new System.IO.StreamReader("encrypted_server_signing_verification_pub_prv.txt"))
            {
                RSAsign = fileReader.ReadLine();
            }
            // Part encrypt
            byte[] RSAByte = hexStringToByteArray(RSAencrypted);
            string RSAstring = Encoding.Default.GetString(RSAByte);
            // Part sign
            byte[] RSASignByte = hexStringToByteArray(RSAsign);
            string RSASignstring = Encoding.Default.GetString(RSASignByte);


            string password;
            using (System.IO.StreamReader fileReader =                      // Keys for verification and signing
new System.IO.StreamReader("encKey.txt"))
            {
                password = fileReader.ReadLine();
            }


            byte[] hashed = hashWithSHA256(password);
            byte[] keyByte = new byte[16];   // get key
            byte[] IVByte = new byte[16];     // get IV

            for (int i = 0; i < 16; i++)        // get IV
            {
                IVByte[i] = hashed[i];
            }

            int counter = 0;

            for (int i = 16; i < 32; i++)       // get key
            {
                keyByte[counter] = hashed[i];
                counter++;
            }

            byte[] decryptedRSAByte = decryptWithAES128(RSAstring, keyByte, IVByte);  // Encryption
            byte[] decryptedRSASignByte = decryptWithAES128(RSASignstring, keyByte, IVByte);  // Sign

            string hexResult = generateHexStringFromByteArray(decryptedRSAByte);  // Encryption
            string hexSignResult = generateHexStringFromByteArray(decryptedRSASignByte);  // Sign

            byte[] resultByte = hexStringToByteArray(hexResult); // Encryption
            byte[] resultSignByte = hexStringToByteArray(hexSignResult); // Sign


            resultRSA = Encoding.Default.GetString(resultByte);  // result Encryption public private together
            resultSignRSA = Encoding.Default.GetString(resultSignByte); // result Sign public private together
            Console.WriteLine(resultRSA);
            Console.WriteLine(resultSignRSA);
        }

        private void listenButton_Click(object sender, EventArgs e)
        {
            int serverPort;
            Thread acceptThread;

            if (Int32.TryParse(clientPort.Text, out serverPort))
            {
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, serverPort));
                serverSocket.Listen(3);

                listening = true;
                listenButton.Enabled = false;
                acceptThread = new Thread(new ThreadStart(Accept));
                acceptThread.Start();

                logs.AppendText("Started listening on port: " + serverPort + "\n");
            }
            else
            {
                logs.AppendText(Environment.NewLine + "Please check port number \n");
            }
        }

        private void Accept()
        {
            while (listening)
            {
                try
                {
                    socketList.Add(serverSocket.Accept());
                    logs.AppendText("A client is connected \n");

                    Thread receiveThread;
                    receiveThread = new Thread(new ThreadStart(Receive));
                    receiveThread.Start();
                }
                catch
                {
                    if (terminating)
                    {
                        listening = false;
                    }
                    else
                    {
                        logs.AppendText("The socket stopped working \n");
                    }
                }
            }
        }

        private void Receive()
        {
            Socket s = socketList[socketList.Count - 1];
            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    Byte[] buffer = new Byte[384];
                    s.Receive(buffer);
                    string incomingMessage = Encoding.Default.GetString(buffer);    // Byte  to string message

                    if (incomingMessage.Contains("AuthenticationRequest"))
                    {
                        RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider(); // Crypto random number
                        var byteArray = new byte[16];
                        provider.GetBytes(byteArray);
                        //convert 4 bytes to an integer
                        randomInteger = BitConverter.ToUInt32(byteArray, 0);


                        authentReqUserName = incomingMessage.Substring(0, incomingMessage.IndexOf("AuthenticationRequest")); // Check if user exists.
                        authReqUser = userList.Find(x => x.name == authentReqUserName);

                        string concat = String.Concat(randomInteger, "RandomNumber");
                        Byte[] randomByte = Encoding.Default.GetBytes(concat);
                        s.Send(randomByte);
                    }
                    else if (incomingMessage.Contains("hmacRNDFromClient"))
                    {
                        string keyString = "hmacRNDFromClient";
                        string Hmac = incomingMessage.Substring(0, incomingMessage.IndexOf(keyString));
                        Byte[] HmacByte = Encoding.Default.GetBytes(Hmac);

                        string rndString = randomInteger.ToString();

                        if (authReqUser != null)
                        {
                            Byte[] toBeCheckedWith = applyHMACwithSHA256(rndString, hexStringToByteArray(authReqUser.password));
                            string result = Encoding.Default.GetString(toBeCheckedWith);

                            if (Hmac.Equals(result))     // Succesful authentication
                            {
                                RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider(); // Crypto random number
                                var byteArray1 = new byte[16];
                                provider.GetBytes(byteArray1);
                                //convert 4 bytes to an integer
                                var randomNumber = BitConverter.ToUInt32(byteArray1, 0);
                                

                                RNGCryptoServiceProvider provider2 = new RNGCryptoServiceProvider(); // Crypto random number 2
                                var byteArray2 = new byte[16];
                                provider2.GetBytes(byteArray2);
                                //convert 4 bytes to an integer
                                var randomNumber2 = BitConverter.ToUInt32(byteArray2, 0);

                                Console.WriteLine(hexStringToByteArray(authReqUser.password));

                                var symEncDecByte = encryptWithAES128(randomNumber.ToString(), hexStringToByteArray(authReqUser.password), byteArray1);

                                var msgAuthKeyByte = encryptWithAES128(randomNumber2.ToString(), hexStringToByteArray(authReqUser.password), byteArray2);

                                var resultForSym = String.Concat("successVerificationAuthentication", Encoding.Default.GetString(symEncDecByte));

                                var resultForAll = String.Concat(resultForSym, Encoding.Default.GetString(msgAuthKeyByte));
 

                                Byte[] messageSent = signWithRSA(resultForAll, 3072, resultSignRSA);
                                string messageResult = Encoding.Default.GetString(messageSent);
                                string messageResultLast = String.Concat(messageResult, resultForAll);
                                Byte[] messageTheLast = Encoding.Default.GetBytes(messageResultLast);
                                s.Send(messageTheLast);
                            }
                            else
                            {
                                string messageToBeSent = "errorVerificationAuthentication";
                                Byte[] messageSent = signWithRSA(messageToBeSent, 3072, resultSignRSA);
                                s.Send(messageSent);
                            }
                        }

                    }

                    Byte[] decryptedRSA = decryptWithRSA(incomingMessage, 3072, resultRSA);
                    string message = "null";
                    if (decryptedRSA != null)
                    {
                        message = Encoding.Default.GetString(decryptedRSA);
                        if (message.Contains("Sending enrollment to server"))
                        {
                            string toBeSearched = "Sending enrollment to server";
                            int length = Int32.Parse(message.Substring(message.IndexOf(toBeSearched) + toBeSearched.Length));
                            string name = message.Substring(0, length);
                            string password = message.Substring(length, message.IndexOf(toBeSearched) - length);

                            if (userList.Exists(x => x.name == name)) // User already exists
                            {

                                string messageToBeSent = "errorVerificationEnrollment";
                                Byte[] messageSent = signWithRSA(messageToBeSent, 3072, resultSignRSA);
                                s.Send(messageSent);
                            }
                            else
                            {
                                var user = new User(name, password);
                                userList.Add(user);
                                string messageToBeSent = "successVerificationEnrollment";
                                Byte[] messageSent = signWithRSA(messageToBeSent, 3072, resultSignRSA);
                                s.Send(messageSent);

                            }
                        }
                    }

                    logs.AppendText(Environment.NewLine + incomingMessage);

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    if (!terminating)
                    {
                        logs.AppendText("A client has disconnected\n");
                    }

                    s.Close();
                    socketList.Remove(s);
                    connected = false;
                }
            }
        }


        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            listening = false;
            terminating = true;
            Environment.Exit(0);
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

        // encryption with AES-128
        static byte[] decryptWithAES128(string input, byte[] key, byte[] IV)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);

            // create AES object from System.Security.Cryptography
            RijndaelManaged aesObject = new RijndaelManaged();
            // since we want to use AES-128
            aesObject.KeySize = 128;
            // block size of AES is 128 bits
            aesObject.BlockSize = 128;
            // mode -> CipherMode.*
            aesObject.Mode = CipherMode.CFB;
            // feedback size should be equal to block size
            // aesObject.FeedbackSize = 128;
            // set the key
            aesObject.Key = key;
            // set the IV
            aesObject.IV = IV;
            // create an encryptor with the settings provided
            ICryptoTransform decryptor = aesObject.CreateDecryptor();
            byte[] result = null;

            try
            {
                result = decryptor.TransformFinalBlock(byteInput, 0, byteInput.Length);
            }
            catch (Exception e) // if encryption fails
            {
                Console.WriteLine(e.Message); // display the cause
            }

            return result;
        }

        // encryption with AES-128
        static byte[] encryptWithAES128(string input, byte[] key, byte[] IV)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);

            // create AES object from System.Security.Cryptography
            RijndaelManaged aesObject = new RijndaelManaged();
            // since we want to use AES-128
            aesObject.KeySize = 128;
            // block size of AES is 128 bits
            aesObject.BlockSize = 128;
            // mode -> CipherMode.*
            aesObject.Mode = CipherMode.CFB;
            // feedback size should be equal to block size
            aesObject.FeedbackSize = 128;
            // set the key
            aesObject.Key = key;
            // set the IV
            aesObject.IV = IV;
            // create an encryptor with the settings provided
            ICryptoTransform encryptor = aesObject.CreateEncryptor();
            byte[] result = null;

            try
            {
                result = encryptor.TransformFinalBlock(byteInput, 0, byteInput.Length);
            }
            catch (Exception e) // if encryption fails
            {
                Console.WriteLine(e.Message); // display the cause
            }

            return result;
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

        // signing with RSA
        static byte[] signWithRSA(string input, int algoLength, string xmlString)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlString);
            byte[] result = null;

            try
            {
                result = rsaObject.SignData(byteInput, "SHA256");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }



        private void ChangePasswordButton_Click(object sender, EventArgs e)
        {
            using (System.IO.StreamReader fileReader =              // Keys for encry and decry
new System.IO.StreamReader("encrypted_server_enc_dec_pub_prv.txt"))
            {
                RSAencrypted = fileReader.ReadLine();
            }

            using (System.IO.StreamReader fileReader =                      // Keys for verification and signing
new System.IO.StreamReader("encrypted_server_signing_verification_pub_prv.txt"))
            {
                RSAsign = fileReader.ReadLine();
            }

            string oldPassword = OldPasswordTextBox.Text;
            string newPassword = NewPasswordTextBox.Text;

            // Part encrypt
            byte[] RSAByte = hexStringToByteArray(RSAencrypted);
            string RSAstring = Encoding.Default.GetString(RSAByte);
            // Part sign
            byte[] RSASignByte = hexStringToByteArray(RSAsign);
            string RSASignstring = Encoding.Default.GetString(RSASignByte);

            byte[] hashed = hashWithSHA256(oldPassword);
            byte[] keyByte = new byte[16];   // get key
            byte[] IVByte = new byte[16];     // get IV

            for (int i = 0; i < 16; i++)        // get IV
            {
                IVByte[i] = hashed[i];
            }

            int counter = 0;

            for (int i = 16; i < 32; i++)       // get key
            {
                keyByte[counter] = hashed[i];
                counter++;
            }

            byte[] decryptedRSAByte = decryptWithAES128(RSAstring, keyByte, IVByte);  // Encryption
            byte[] decryptedRSASignByte = decryptWithAES128(RSASignstring, keyByte, IVByte);  // Sign

            string hexResult = generateHexStringFromByteArray(decryptedRSAByte);  // Encryption
            string hexSignResult = generateHexStringFromByteArray(decryptedRSASignByte);  // Sign

            byte[] resultByte = hexStringToByteArray(hexResult); // Encryption
            byte[] resultSignByte = hexStringToByteArray(hexSignResult); // Sign


            resultRSA = Encoding.Default.GetString(resultByte);  // result Encryption public private together
            resultSignRSA = Encoding.Default.GetString(resultSignByte); // result Sign public private together

            ///////////////ENCRYPTION PART////////////////

            byte[] hashedEncryption = hashWithSHA256(newPassword);
            byte[] keyByteEncryption = new byte[16];   // get key
            byte[] IVByteEncryption = new byte[16];     // get IV

            for (int i = 0; i < 16; i++)        // get IV
            {
                IVByteEncryption[i] = hashedEncryption[i];
            }

            int counterEncryption = 0;

            for (int i = 16; i < 32; i++)       // get key
            {
                keyByteEncryption[counterEncryption] = hashedEncryption[i];
                counterEncryption++;
            }

            byte[] encryptedRSAByte = encryptWithAES128(resultRSA, keyByteEncryption, IVByteEncryption);  // Encryption
            byte[] encryptedRSASignByte = encryptWithAES128(resultSignRSA, keyByteEncryption, IVByteEncryption);  // Sign

            string hexResultEncrypt = generateHexStringFromByteArray(encryptedRSAByte);  // Encryption
            string hexSignResultEncrypt = generateHexStringFromByteArray(encryptedRSASignByte);  // Sign

            byte[] resultByteEncrypt = hexStringToByteArray(hexResultEncrypt); // Encryption
            byte[] resultSignByteEncrypt = hexStringToByteArray(hexSignResultEncrypt); // Sign


            using (StreamWriter sw = new StreamWriter("encKey.txt"))   // Write to files.
            {

                sw.Write(newPassword);
            }


            using (StreamWriter sw = new StreamWriter("encrypted_server_enc_dec_pub_prv.txt"))   // Write to files.
            {

                sw.Write(hexResultEncrypt);
            }

            using (StreamWriter sw = new StreamWriter("encrypted_server_signing_verification_pub_prv.txt"))   // Write to files.
            {

                sw.Write(hexSignResultEncrypt);
            }
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

        public bool Equality(byte[] a1, byte[] b1)
        {
            int i;
            if (a1.Length == b1.Length)
            {
                i = 0;
                while (i < a1.Length && (a1[i] == b1[i])) //Earlier it was a1[i]!=b1[i]
                {
                    i++;
                }
                if (i == a1.Length)
                {
                    return true;
                }
            }

            return false;
        }

    }
}

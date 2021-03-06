﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TFStackEmulator.Devices;

namespace TFStackEmulator
{
    public class StackEmulator
    {
        public static readonly UID BroadcastUID = new UID(0);

        public event ResponseEventHandler Response;
        public delegate void ResponseEventHandler(object sender, ResponseEventArgs args);

        private Dictionary<UID, Device> Devices = new Dictionary<UID, Device>();
        private bool RunSimulation;

        private Thread SimulationThread;

        private BlockingConcurrentQueue<Packet> RequestQueue = new BlockingConcurrentQueue<Packet>();

        public StackEmulator()
        {
            PrepareSimulation();
        }

        private void PrepareSimulation()
        {
            RunSimulation = true;

            SimulationThread = new Thread(SimulationLoop);
            SimulationThread.Name = "Stack-Simulation";
            SimulationThread.IsBackground = true;
        }

        private void SimulationLoop()
        {
            PacketSink sink = new DelegatePacketSink(OnResponse);
            while (RunSimulation)
            {
                Packet request;
                if (RequestQueue.TryDequeue(out request, 1))
                {
                    RouteAndDispatchRequest(request);
                }

                foreach (var device in Devices.Values)
                {
                    device.OnTick(sink);
                }
            }
        }

        private void RouteAndDispatchRequest(Packet packet)
        {
            try
            {
                TryRouteAndDispatchRequest(packet);
            }
            catch (Exception e)
            {
                Console.WriteLine("An unexpected and unhandled error occured while handling a request:");
                Console.WriteLine(e);
            }
        }

        private void TryRouteAndDispatchRequest(Packet packet)
        {
            if (packet.UID.Equals(BroadcastUID))
            {
                foreach (Device device in Devices.Values)
                {
                    DispatchRequest(packet, device);
                }
            }
            else
            {
                Device device;
                if (Devices.TryGetValue(packet.UID, out device))
                {
                    DispatchRequest(packet, device);
                }
                else
                {
                    Console.WriteLine("No Device for UID {0}", packet.UID);
                }
            }
        }

        private void DispatchRequest(Packet packet, Device device)
        {
            Packet response = device.HandleRequest(packet.Copy());
            if (response != null)
            {
                OnResponse(response);
            }
        }

        protected void OnResponse(Packet response)
        {
            var handler = Response;
            if (handler != null)
            {
                handler(this, new ResponseEventArgs(response));
            }
        }

        public void Start()
        {
            if (SimulationThread.IsAlive)
            {
                throw new InvalidOperationException("Cannot start the simulation when it is already running");
            }

            SimulationThread.Start();
        }

        public void Stop()
        {
            RunSimulation = false;
            SimulationThread.Join();
            PrepareSimulation();
        }

        public void AddDevice(Device device)
        {
            Devices.Add(device.UID, device);
        }

        public void HandleRequest(Packet packet)
        {
            RequestQueue.Enqueue(packet);
        }
    }

    public class ResponseEventArgs : EventArgs
    {
        public Packet Response { get; private set; }

        public ResponseEventArgs(Packet response)
        {
            Response = response;
        }
    }
}

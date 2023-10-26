using System.IO.Ports;

namespace EdcHost.SlaveServers;

class SerialPortHub : ISerialPortHub
{
    public List<string> PortNames => SerialPort.GetPortNames().ToList();

    public ISerialPortWrapper Get(string portName) => new SerialPortWrapper(portName);
}

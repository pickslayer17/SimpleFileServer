using ConsoleApp3;

var path = "D:\\WorkSpace\\AUTOMATION\\DealCloud.Tests.Automation\\DealCloud.Tests.AddIns.Tests\\bin\\Debug\\AddIns\\AddIns\\Excel\\Prev1";
var server = FileServer.Start(path, 54543);
Console.WriteLine( server.Url);
Console.ReadLine();
var path2 = "D:\\WorkSpace\\AUTOMATION\\DealCloud.Tests.Automation\\DealCloud.Tests.AddIns.Tests\\bin\\Debug\\AddIns\\AddIns\\Excel\\Current";
server.ChangeFolder(path2);
Console.ReadLine();
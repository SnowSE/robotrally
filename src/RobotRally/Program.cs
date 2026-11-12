using RobotRally;

var app = RobotRallyServer.Create(args);
app.Lifetime.ApplicationStarted.Register(() => RobotRallyServer.PrintBanner(app));
app.Run();

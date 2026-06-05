using Godot;
using KBEngine;

public sealed class ClientKbeLogProvider : ILogProvider
{

	public void Info(string message)
	{
		GD.Print(message);
	}

	public void Warn(string message)
	{
		GD.PushWarning(message);
	}

	public void Error(string message)
	{
		GD.PrintErr(message);
	}

	public void Debug(string message)
	{
		GD.Print(message);
	}

}

// See https://aka.ms/new-console-template for more information
using System;
using OwlTree;

class Program
{
    static void Main(string[] args)
    {
        if (args[0] == "s")
        {
            new Connection(new Connection.ConnectionArgs
            {
                role = Connection.Role.Server
            });
        }
        else if (args[0] == "c")
        {
            new Connection(new Connection.ConnectionArgs
            {
                role = Connection.Role.Client
            });
        }
    }
}

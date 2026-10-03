global using Xunit;

// The tests deliberately reach into Bloxstrap's internals (Paths, App state,
// Bootstrapper cleanup) rather than only its public surface - that is where the
// behaviour being asserted actually lives.
global using Bloxstrap;
global using Bloxstrap.Enums;
global using Bloxstrap.Models.Entities;
global using Bloxstrap.Resources;
global using Bloxstrap.RobloxInterfaces;
global using System.IO;
global using System.Windows;
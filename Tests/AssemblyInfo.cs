using Xunit;

// Rainstrap keeps its paths, settings and Roblox state in static singletons by
// design - a single running client owns them. The tests redirect those singletons
// at a temporary directory, which is the only way to exercise the file-level
// behaviour without touching the developer's real Roblox installation, and it means
// the tests cannot run concurrently. Serialising them is a correctness
// requirement here, not a preference.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
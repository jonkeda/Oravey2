using Xunit;

// Disable parallel test execution — each test class launches a game process
// using the same named pipe, so they must run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

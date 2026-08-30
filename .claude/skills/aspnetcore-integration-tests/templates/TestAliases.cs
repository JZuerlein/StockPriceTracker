// TEMPLATE - aspnetcore-integration-tests skill.
// ============================================================================================
// THIS IS THE ONLY FILE THAT NAMES YOUR APPLICATION'S TYPES.
// Point the two aliases at your app, and every other infrastructure file in this project
// compiles unchanged — no find-and-replace, nothing to re-do when you copy them to the next
// project. Set these first; nothing else builds until they resolve.
// ============================================================================================

// The entry point WebApplicationFactory<T> boots. Either your app's `public partial class
// Program { }`, or a TestProgram the test project defines (see examples/ExampleTestProgram.cs
// and step 1 of SKILL.md for which to pick).
global using TestEntryPoint = Program;

// Your EF Core DbContext. The fixture seeds through it and ExecuteDbContextAsync hands it to
// tests for database-level assertions.
global using TestDbContext = AppDbContext;

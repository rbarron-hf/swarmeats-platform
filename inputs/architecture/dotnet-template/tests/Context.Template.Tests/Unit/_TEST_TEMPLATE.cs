using FluentAssertions;
using Moq;
using Xunit;
using {ContextName}.Domain.Aggregates;
using {ContextName}.Domain.Commands;
using {ContextName}.Domain.Exceptions;
using {ContextName}.Infrastructure.Repositories;

namespace {ContextName}.Tests.Unit;

/// <summary>
/// Unit tests for {STORY_ID}: {story title}.
///
/// Story: {STORY_ID}
/// AC Node: {AC_NODE_ID}
///
/// Test naming convention: MethodOrScenario_Condition_ExpectedResult
/// Each test covers one acceptance criterion or business rule.
/// </summary>
public sealed class {STORY_ID}Tests
{
    // ── Shared fixtures ──

    private readonly Mock<I{Aggregate}Repository> _repositoryMock;

    public {STORY_ID}Tests()
    {
        _repositoryMock = new Mock<I{Aggregate}Repository>(MockBehavior.Strict);
    }

    // ── Helper factory methods ──

    /// <summary>
    /// Creates a valid aggregate in the expected pre-condition state.
    /// Centralise construction so tests focus on the behaviour under test.
    /// </summary>
    private static {Aggregate} CreateValid{Aggregate}(
        string id = "test-id-001",
        /* other params */)
    {
        // TODO: Build a valid aggregate matching preconditions
        return new {Aggregate}(id /* , ... */);
    }

    // ══════════════════════════════════════════════════════════════
    // Happy-path tests
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Handle_{ValidScenario}_Returns{ExpectedResult}()
    {
        // Arrange
        var aggregate = CreateValid{Aggregate}();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(aggregate);
        _repositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<{Aggregate}>()))
            .Returns(Task.CompletedTask);

        var handler = new {CommandOrQuery}Handler(_repositoryMock.Object);
        var command = new {CommandOrQuery} { Id = "test-id-001" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // TODO: Assert specific response properties
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<{Aggregate}>()), Times.Once);
    }

    [Fact]
    public void {Aggregate}_{ValidAction}_TransitionsToExpectedState()
    {
        // Arrange
        var aggregate = CreateValid{Aggregate}();

        // Act
        aggregate.{TransitionMethod}(/* params */);

        // Assert
        aggregate.Status.Should().Be(/* expected status */);
    }

    // ══════════════════════════════════════════════════════════════
    // Business rule validation tests
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void {Aggregate}_{BusinessRuleViolation}_ThrowsInvalid{Aggregate}Exception()
    {
        // Arrange — construct aggregate at boundary of rule
        // e.g., for minimum order £10: set total to £9.99

        // Act
        var act = () => new {Aggregate}(/* invalid params */);

        // Assert
        act.Should().Throw<Invalid{Aggregate}Exception>()
            .Which.ErrorCode.Should().Be("{ENTITY}_{RULE_CODE}");
    }

    [Theory]
    [InlineData(/* boundary value 1 */)]
    [InlineData(/* boundary value 2 */)]
    public void {Aggregate}_{BoundaryTest}_ValidatesCorrectly(/* params */)
    {
        // Arrange & Act & Assert
        // TODO: Test boundary conditions (min/max values, edge cases)
    }

    // ══════════════════════════════════════════════════════════════
    // Invalid state transition tests
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void {Aggregate}_{InvalidTransition}_ThrowsInvalid{Aggregate}StateException()
    {
        // Arrange — put aggregate in a state that disallows the transition
        var aggregate = CreateValid{Aggregate}();
        // Force into invalid precondition state if needed

        // Act
        var act = () => aggregate.{TransitionMethod}(/* params */);

        // Assert
        act.Should().Throw<Invalid{Aggregate}StateException>()
            .Which.ErrorCode.Should().Be("{ENTITY}_INVALID_TRANSITION");
    }

    [Fact]
    public void {Aggregate}_{InvalidTransition}_DoesNotMutateState()
    {
        // Arrange
        var aggregate = CreateValid{Aggregate}();
        var originalStatus = aggregate.Status;

        // Act
        try { aggregate.{TransitionMethod}(/* params */); }
        catch (Invalid{Aggregate}StateException) { /* expected */ }

        // Assert — aggregate is unchanged (zero side-effects on rejection)
        aggregate.Status.Should().Be(originalStatus);
    }

    // ══════════════════════════════════════════════════════════════
    // Not-found / null tests
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Handle_{NotFoundScenario}_ThrowsNotFoundException()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((({Aggregate}?)null));

        var handler = new {CommandOrQuery}Handler(_repositoryMock.Object);
        var command = new {CommandOrQuery} { Id = "nonexistent-id" };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<{Aggregate}NotFoundException>();
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<{Aggregate}>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════
    // Domain event tests
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void {Aggregate}_{Action}_RaisesDomainEvent()
    {
        // Arrange
        var aggregate = CreateValid{Aggregate}();

        // Act
        aggregate.{TransitionMethod}(/* params */);

        // Assert
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<{EventName}>();
    }
}

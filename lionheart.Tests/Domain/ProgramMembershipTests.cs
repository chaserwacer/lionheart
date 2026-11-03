using lionheart.Model.Training.Sessions;

namespace lionheart.Tests.Domain;

public class ProgramMembershipTests
{
    [Fact]
    public void InProgram_carries_its_program_id()
    {
        var programId = Guid.NewGuid();

        Assert.Equal(programId, new InProgram(programId).TrainingProgramId);
    }

    [Fact]
    public void InProgram_rejects_the_empty_guid_reserved_for_standalone() =>
        Assert.Throws<ArgumentException>(() => new InProgram(Guid.Empty));
}

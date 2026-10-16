namespace PlotTwist.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public void Migrating_an_up_to_date_database_is_a_no_op() => Assert.False(postgres.Migrate());
}

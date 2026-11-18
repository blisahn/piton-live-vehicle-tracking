using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VehicleTracking.Infrastructure.Persistence.Migrations;

/// <summary>Hand-written like <see cref="Initial"/>. Accounts and refresh tokens; retention does not touch them.</summary>
[DbContext(typeof(TrackingDbContext))]
[Migration("20261118000000_Auth")]
public sealed class AuthSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // email is stored lower-cased by the application, so a plain unique constraint is enough.
        migrationBuilder.Sql("""
            create table users (
                id uuid primary key,
                email text not null unique,
                password_hash text not null,
                role text not null,
                created_at timestamptz not null
            );
            """);

        // Only the SHA-256 of a token is stored. All tokens of one login share family_id.
        migrationBuilder.Sql("""
            create table refresh_tokens (
                id uuid primary key,
                user_id uuid not null references users (id) on delete cascade,
                family_id uuid not null,
                token_hash text not null unique,
                created_at timestamptz not null,
                expires_at timestamptz not null,
                revoked_at timestamptz,
                replaced_by_id uuid
            );
            """);

        migrationBuilder.Sql("create index ix_refresh_tokens_family_id on refresh_tokens (family_id);");
        migrationBuilder.Sql("create index ix_refresh_tokens_user_id on refresh_tokens (user_id);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("drop table if exists refresh_tokens;");
        migrationBuilder.Sql("drop table if exists users;");
    }
}

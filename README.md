# SchemaForge

SchemaForge is a cross-platform desktop database workspace designed for developers who need a practical, organized, and reliable way to work with relational databases.

It follows the general idea of tools like DBeaver and DataGrip, allowing users to connect to databases, explore schemas, inspect tables, and run SQL queries. Its long-term focus, however, goes beyond query execution: SchemaForge is being built to help developers understand, track, and manage database structure evolution through snapshots, schema comparison, and migrations.

## Current Scope

At its current stage, SchemaForge provides the foundation for a desktop database client with support for:

- PostgreSQL connections
- MySQL connections
- Local connection persistence
- Schema exploration
- Table preview
- Basic query execution
- Internal application storage using SQLite

## Vision

SchemaForge is not meant to be just another SQL client.

The goal is to provide a cleaner and more focused database workspace where developers can:

- connect to local or remote databases
- browse schemas, tables, and columns
- inspect and preview data
- execute and organize SQL queries
- save structural snapshots of a database
- compare database states over time
- manage and apply migrations with more clarity

In short, SchemaForge aims to combine the daily usefulness of a database client with the structural awareness needed for long-term database maintenance.

## Tech Stack

### Desktop
- Tauri
- React
- TypeScript

### Local Engine
- .NET 8

### Internal Storage
- SQLite

### Supported Databases
- PostgreSQL
- MySQL

## Architecture

The project is organized as a monorepo with a layered structure:

```text
apps/desktop-ui              # React + TypeScript frontend
src-tauri                    # Tauri desktop shell
core/SchemaForge.Domain      # Domain models and core entities
core/SchemaForge.Application # Contracts, services, use cases
core/SchemaForge.Infrastructure # Database providers, SQLite storage, implementations
core/SchemaForge.Api         # Local engine consumed by the desktop app
```

This structure is intended to keep the project maintainable, testable, and ready to grow as new features are added.

## Project Goals

SchemaForge is being built with the following principles in mind:

- clear separation of concerns
- maintainable architecture
- practical developer experience
- clean desktop UX
- support for local and remote relational databases
- strong foundation for future schema management features

## Roadmap

### Phase 1
- Project structure
- Desktop shell
- Local engine
- Connection management foundation

### Phase 2
- Database explorer
- Schema, tables, and columns browsing
- Table preview
- Basic query runner

### Phase 3
- Saved queries
- Improved query history
- Schema snapshots

### Phase 4
- Schema diff
- Structural comparison between snapshots

### Phase 5
- Migration management
- Apply / rollback workflow
- Migration history

## Why SchemaForge

Many database tools are powerful, but often broad and overloaded.

SchemaForge is being built with a more focused approach:
- provide the essential features developers actually use every day
- keep the interface clean and direct
- add meaningful structural tools that help track database evolution over time

The intent is to make database work feel more organized, more transparent, and easier to maintain.

## Status

SchemaForge is currently under active development.
The current implementation should be treated as a strong starter foundation rather than a finished product.

More features and refinements are planned as the project evolves.

## Future Ideas

Some planned or possible future improvements include:

- saved query collections
- schema snapshot history
- visual schema comparison
- migration runner
- read-only connection mode
- export tools
- query result history
- additional database providers such as SQL Server and Oracle


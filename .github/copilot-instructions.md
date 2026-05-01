# Vulscan Copilot Instructions

This workspace uses instruction files in `.github/instructions/*.instructions.md` with `applyTo: "**"` so Copilot Chat automatically includes them for all requests.

- Documentation: `.github/instructions/documentation.instructions.md` (applies to `wiki, wiki pages, *.md,*.markdown, *.rst,*.adoc, *.asciidoc,*.txt, *.`)

VS Code automatically includes this file when `github.copilot.chat.codeGeneration.useInstructionFiles` is enabled.

---

## Code Generation Standards

### Completeness Requirements

1. **Never Leave Code Incomplete**
   - Always implement complete, production-ready code
   - No placeholder comments like "TODO: implement this" or "... rest of the code"
   - No stub methods that throw NotImplementedException
   - Complete all error handling, validation, and edge cases

2. **Implementation Standards**
   - Implement all methods, classes, and interfaces fully
   - Include proper error handling with try-catch blocks where appropriate
   - Add comprehensive input validation
   - Handle null checks and edge cases
   - Include proper logging statements

### Best Practices

1. **Architecture & Design**
   - Follow Clean Architecture principles (Domain, Application, Infrastructure, API layers)
   - Use dependency injection for all services
   - Apply SOLID principles
   - Use async/await properly with CancellationToken support
   - Implement proper separation of concerns

2. **Security**
   - Never expose sensitive data in API responses (passwords, tokens, etc.)
   - Validate all user inputs
   - Use parameterized queries to prevent SQL injection
   - Implement proper authentication and authorization
   - Encrypt sensitive data in database (passwords, API keys)
   - Follow OWASP security guidelines

3. **Performance**
   - Use Entity Framework efficiently (avoid N+1 queries)
   - Implement pagination for large datasets
   - Use caching where appropriate
   - Optimize database queries with proper indexes
   - Use async operations for I/O-bound work
   - Avoid unnecessary database round-trips

4. **Code Quality**
   - Write clean, readable, maintainable code
   - Use meaningful variable and method names
   - Add XML documentation comments to public APIs
   - Follow C# coding conventions
   - Use records for DTOs
   - Implement proper null handling
   - Use pattern matching where appropriate

5. **TypeScript & Angular Best Practices**
   - **Strict Typing:** Always use interfaces or types. **Strictly avoid `any`.**
   - **Standalone Architecture:** Use `standalone: true` for all components, directives, and pipes.
   - **Component Scope:** Keep components small, focused, and single-purpose.
   - **File Structure:** Maintain separate files for models, services, and components.
   - **SoC (Separation of Concerns):** Keep templates (HTML), styles (SCSS), and logic (TS) in distinct files.
   - **Reactive Programming:** Use **RxJS** for asynchronous data streams and event handling.
   - **UI Framework:** Consistently use **Angular Material** components for all UI elements.
   - **State Management:** Use **Angular Signals** for local and global state (Angular 19+ standards).
   - **HTTP Reliability:** Implement explicit error handling (e.g., `catchError`) for all HTTP service calls.
   - **Naming Conventions:** Strictly follow the official **Angular Style Guide**.

### Testing & Validation Workflow

1. **Build After Every Change**
   - Run `dotnet build` after backend changes
   - Run `ng build` or check for TypeScript errors after frontend changes
   - Fix all compilation errors immediately
   - Address warnings when possible

2. **Iterative Development**
   - Make incremental changes
   - Test after each significant change
   - Fix errors before moving to next feature
   - Don't accumulate technical debt

3. **Quality Checks**
   - Verify all endpoints work correctly
   - Check database migrations apply successfully
   - Validate API contracts match between frontend and backend
   - Ensure proper error handling is in place
   - Test edge cases and error scenarios

### Completion Reports

After completing any significant work, provide a structured report with:

1. **Summary of Changes**
   - List all files created
   - List all files modified
   - Briefly describe what each change does

2. **Build Status**
   - Report successful compilation
   - Note any warnings that remain
   - Confirm database migrations applied

3. **Testing Recommendations**
   - Suggest manual testing steps
   - Identify key scenarios to verify
   - Note any automated tests needed

4. **Next Steps**
   - Suggest improvements or enhancements
   - Identify related work that could be done
   - Note any technical debt or follow-up items

### Example Workflow

When implementing a new feature:

1. **Plan** - Understand requirements, identify all components needed
2. **Implement Backend** - Domain entities, services, API controllers
3. **Build & Test** - `dotnet build`, fix errors, test APIs
4. **Implement Frontend** - Models, services, components, UI
5. **Build & Test** - Check TypeScript, test UI functionality
6. **Integration** - Wire up backend and frontend
7. **Final Validation** - Test complete workflow end-to-end
8. **Documentation** - Update relevant docs, add comments
9. **Report** - Provide completion report with focused points

### Quality Gates

Before considering any work complete:

- ✅ All code compiles without errors
- ✅ No critical warnings remain
- ✅ All interfaces fully implemented
- ✅ Error handling is comprehensive
- ✅ Database migrations created and applied
- ✅ API contracts match between frontend/backend
- ✅ Code follows project conventions
- ✅ Security best practices followed
- ✅ Performance considerations addressed
- ✅ Documentation updated

---

## Technology Stack Reference

### Backend

- .NET 10 with ASP.NET Core
- Entity Framework Core (SQL Server)
- Clean Architecture pattern
- JWT authentication
- Serilog for logging

### Frontend

- Angular 19+ (standalone components)
- TypeScript (strict mode)
- Angular Material UI
- RxJS for reactive programming
- Signals for state management

### Database

- SQL Server (production)
- SQLite (development option)
- Entity Framework migrations

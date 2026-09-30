# StudySprint

StudySprint is a secure web-based study-session planner that helps students organize subjects, set focused goals, manage study sessions, and track completed work.

## Live Application

- **Live site:** https://hosjoh.github.io/StudySprint/
- **Final presentation:** [Add the recording URL here before final submission]

## Key Features

- User registration and password-based sign-in
- Password changes with validation and reauthentication
- Subject management with add, edit, delete, and wildcard search
- Study-session planning with subject, goal, date, and duration
- Complete/incomplete study-session tracking
- Progress metrics for completed sessions and study minutes
- Responsive browser interface
- GitHub Actions deployment to GitHub Pages

## Technology Stack

- **Frontend:** Blazor WebAssembly, C#, HTML, CSS
- **Authentication:** Supabase Auth
- **Database:** Supabase PostgreSQL
- **Data access:** Supabase REST Data API
- **Hosting:** GitHub Pages
- **CI/CD:** GitHub Actions
- **Version control:** Git and GitHub

## Database

StudySprint uses the following application tables:

- `profiles` — user profile information tied to Supabase Auth
- `subjects` — subjects owned by an authenticated user
- `study_sessions` — study goals, date, duration, subject association, and completion state

The `auth.users` table is managed by Supabase Auth.

## Security

StudySprint uses security controls beyond password complexity:

- Supabase Auth manages credentials and password hashing.
- PostgreSQL Row Level Security restricts users to their own application records.
- Anonymous database privileges are removed from protected tables.
- Authenticated database calls use the signed-in user's bearer token.
- Input values are validated before database requests are submitted.
- Password changes require verification of the current password.
- The browser session is stored in `sessionStorage` so login state is not intentionally persisted indefinitely.
- GitHub Pages and Supabase are accessed over HTTPS.
- Only the Supabase publishable key belongs in the browser application; secret/service-role keys must never be committed.

## Project Structure

```text
StudySprint/
├── Layout/              # Shared navigation and application layout
├── Models/              # Data models returned by Supabase
├── Pages/               # Blazor pages
├── Services/            # Auth, database, and browser session services
├── wwwroot/             # Static assets, CSS, and application configuration
├── database-schema.sql  # PostgreSQL schema and RLS policies
└── .github/workflows/   # GitHub Pages deployment workflow
```

## Local Development

### Prerequisites

- Visual Studio Community with ASP.NET and web development workload
- .NET 10 SDK and WebAssembly build tools
- Git
- A Supabase project

### Configuration

Create/update `wwwroot/appsettings.json`:

```json
{
  "Supabase": {
    "Url": "https://YOUR_PROJECT.supabase.co",
    "PublishableKey": "sb_publishable_YOUR_KEY"
  }
}
```

Use only a publishable key in the browser application.

### Run Locally

1. Open the StudySprint solution in Visual Studio Community.
2. Build the solution.
3. Run the HTTPS launch profile.
4. Register or sign in with a test account.

## Redeployment / Recovery

StudySprint is designed so the frontend can be redeployed from the GitHub repository.

1. Clone or fork this repository.
2. Install the .NET 10 SDK and WebAssembly workload.
3. Configure `wwwroot/appsettings.json` with the target Supabase Project URL and publishable key.
4. Apply `database-schema.sql` to the Supabase project if deploying against a new database.
5. Build locally with `dotnet publish` to verify the project.
6. In GitHub, enable **Pages → GitHub Actions** as the publishing source.
7. Push to `main`; the workflow under `.github/workflows/` builds and deploys the static Blazor output.
8. Verify login, database access, search, session management, and progress on the new deployment.

This process allows the web frontend to be recreated from version-controlled source rather than relying on a single local machine.

## Version Control

Development was organized into milestone tags:

- `Phase-1`
- `Phase-2`
- `Phase-3`
- `Final`

Commit messages describe functional milestones and fixes so the repository history documents the development process.

## Testing

The final project should be tested with cases including:

- Correct and incorrect login credentials
- Weak and mismatched registration passwords
- Blank or invalid form inputs
- Wildcard and no-result searches
- Add, edit, and delete subject operations
- Add, complete, edit, and delete study sessions
- Signed-out access to protected functionality
- Page refresh after login
- Progress calculations after session changes

## Future Enhancements

Potential future improvements include study reminders, calendar integration, richer analytics, a Pomodoro timer, multi-factor authentication, and additional accessibility refinements.

## Project Summary

StudySprint demonstrates a complete browser-based CRUD application built with Blazor WebAssembly and a PostgreSQL backend. The project combines authentication, user-specific database authorization, wildcard search, study-session management, progress tracking, responsive interface design, Git version control, documented deployment, and automated GitHub Pages publishing. It was developed as a portfolio-ready example of designing, securing, documenting, testing, and deploying a small full-stack web application.

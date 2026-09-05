// Run E2E tests serially (not method-level parallel). The suite signs in through the
// real MFA flow using a small set of shared seeded accounts; the backend enforces TOTP
// anti-replay (a code consumed in its 30s step can't be reused). Parallel logins as the
// same fixture user collide on that, so we serialize — combined with the next-step retry
// in E2ETestBase.SignInAsync, same-user logins resolve deterministically one at a time.
[assembly: DoNotParallelize]

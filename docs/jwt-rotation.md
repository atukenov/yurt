# Rotating the JWT secret

The server signs every login token with a secret, set in the `Jwt__Secret` environment
variable. If that secret ever leaks, anyone who has it can forge a valid token, so you
need to change it. This page shows how to change it **without logging everyone out at a
bad moment**, and how to force a full re-login when you want one.

## The two kinds of token (why this is safe)

| Token | Lives | Signed with the secret? |
|---|---|---|
| **Access token** | ~15 minutes | Yes — this is what a secret change affects |
| **Refresh token** | 7 days | No — it is a random value stored in the database |

Because refresh tokens are **not** signed with the JWT secret, changing the secret does
**not** throw users out of the app. Their app notices its access token stopped working,
quietly uses its refresh token to get a new one (now signed with the new secret), and the
person never sees it. The only risk is the ~15-minute window right after the switch, where
access tokens signed with the *old* secret are still in people's hands. The staged
rotation below removes even that bump.

## Staged rotation (recommended, no interruption)

You keep the old secret valid for one short window while the new one takes over.

1. **Generate a new secret** (at least 32 characters):
   ```
   openssl rand -base64 48
   ```
2. **Set the previous secret, then the new one.** Put the *current* secret into
   `Jwt__PreviousSecret`, and the new value into `Jwt__Secret`:
   ```
   Jwt__PreviousSecret = <the secret that is live right now>
   Jwt__Secret         = <the new secret from step 1>
   ```
   Deploy. From now on the server **signs** new tokens with `Jwt__Secret`, but still
   **accepts** tokens signed with either secret. So nobody is interrupted: old access
   tokens keep validating until they expire, new ones use the new secret.
3. **Wait out the window.** Access tokens last ~15 minutes
   (`Jwt__AccessTokenMinutes`). Wait comfortably longer — say an hour — so every old token
   has expired and been replaced.
4. **Remove the previous secret.** Clear `Jwt__PreviousSecret` (delete the variable or set
   it empty) and deploy again. The old secret is now dead; a leaked copy is useless.

`Jwt__PreviousSecret` is only read during a rotation. Leaving it set permanently would
keep the old secret valid forever, which defeats the point — so clear it in step 4. It is
validated the same way as the main secret (at least 32 characters, not the placeholder).

## Hard cutover (one step, small bump)

If you cannot do the staged version — for example you must kill the old secret **this
second** because it leaked — just set the new `Jwt__Secret` and deploy, with no
`Jwt__PreviousSecret`. Every access token signed with the old secret stops working at once.
Apps recover on their own via refresh within a few seconds to a few minutes, but a user
who is mid-request may see one error and a retry. This is the right choice during an active
leak.

## Forcing everyone to sign in again

Rotating the secret does **not** end sessions, because refresh tokens survive it. When you
actually want every user signed out — a stolen device, a departed staff member, a
confirmed breach — revoke the refresh tokens too:

```
dotnet Yurt.WebApi.dll revoke-all-sessions
```

This marks every stored refresh token as revoked. After each user's current access token
expires (~15 min), their app can no longer refresh and they are sent to the login screen.
For an immediate, total lockout: run `revoke-all-sessions` **and** do a hard-cutover secret
change at the same time.

## Checklist

| Goal | Steps |
|---|---|
| Routine rotation, no interruption | Staged rotation (4 steps above) |
| Secret leaked, kill it now | Hard cutover + `revoke-all-sessions` |
| Sign everyone out, keep the same secret | `revoke-all-sessions` only |

## Related

- [admin-password-reset.md](./admin-password-reset.md) — resetting a locked-out account.
- Platform → Integrations shows whether a JWT secret is configured (never its value).

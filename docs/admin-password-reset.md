# Resetting an admin password

Passwords are stored as a **bcrypt hash**. A hash is one-way: nobody — not you, not
the server, not Claude — can read the original password back out of it. The only safe
thing to do with a forgotten password is to **set a new one**. This page shows the two
ways to do that.

> We never try to "decode" or "crack" a stored hash. If someone hands you a hash and
> asks what the password was, the answer is: set a new password instead.

## Which way do I need?

| Situation | Use |
|---|---|
| A worker or cafe admin forgot their password, **and a cafe admin can still sign in** | The staff page (reset from the app) |
| The **only** admin for a cafe is locked out, or the super admin is locked out, so nobody can sign in | The server command |
| An admin just needs to change their own password | They sign in and use **Change password** |

## 1. Reset from the app (normal case)

Any admin with the **Admin** or **Super admin** role can reset a worker's password from
the staff page, as long as that admin can still sign in.

- Admin app → **Staff** → pick the person → **Reset password** → type the new one.
- The worker signs in with the new password right away.
- This is the `POST /api/v1/admin/workers/{id}/reset-password` endpoint behind the screen.

Limits (on purpose): it only resets **workers**, never a super admin, and only inside
**that admin's own cafe**. So it cannot help when the locked-out person is the only admin,
or is the super admin. For that, use the server command below.

## 2. Reset on the server (locked-out admin or super admin)

This is a one-off command you run on the backend host (for example a Railway **one-off
command**, or a shell on the server). It talks straight to the database, so it works even
when nobody can sign in. It needs the same `DATABASE_URL` the server already uses.

**Find the username first** (no passwords or hashes are ever shown):

```
dotnet Yurt.WebApi.dll list-admins
```

```
USERNAME                 ROLE         ACTIVE  MUSTCHANGE  TENANT
alice                    Admin        True    False       3f2a...
root                     SuperAdmin   True    False       0000...
```

**Reset the password.** Pipe the new password in on stdin so it never shows up in the
command history or the process list:

```
echo -n 'the-new-password' | dotnet Yurt.WebApi.dll reset-admin-password alice
```

What it does:

- Sets a fresh bcrypt hash for that account.
- Turns on **"must change password"**, so the person is forced to pick their own new
  password the first time they sign in.
- Re-activates the account if it had been switched off.
- Prints a confirmation. It never prints the password or the hash.

The new password must be at least 8 characters.

### Alternatives for passing the password

- **Interactive** (you are at a real terminal): run it with no pipe and it will prompt
  twice, hiding what you type.
  ```
  dotnet Yurt.WebApi.dll reset-admin-password alice
  New password: ********
  Confirm password: ********
  ```
- **Environment variable** (some one-off runners cannot pipe stdin): set
  `YURT_NEW_PASSWORD` for that single command. Unset it again afterwards.

### After the reset

The person can sign in immediately. Any access token they still hold keeps working until
it expires (about 15 minutes). If you need to sign the account out everywhere **right now**
— for example you think the old password leaked — also run:

```
dotnet Yurt.WebApi.dll revoke-all-sessions
```

That revokes every stored refresh token, so everyone has to sign in again once their
current short-lived token runs out. See [jwt-rotation.md](./jwt-rotation.md) if you also
want to rotate the signing secret.

## If you suspect a password or secret leaked

1. Reset the affected account's password (above).
2. Run `revoke-all-sessions` to end existing sessions.
3. If a **JWT secret** may have leaked, rotate it — see [jwt-rotation.md](./jwt-rotation.md).
4. If a **cafe integration secret** (Stripe, iiko, Webkassa, …) may have leaked, rotate it
   at the provider and re-enter it under Platform → Integrations.

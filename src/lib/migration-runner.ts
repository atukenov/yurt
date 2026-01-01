/**
 * Automatic Migration Runner
 * Runs pending migrations on application startup
 * Can be imported and called during app initialization
 */

import mongoose from "mongoose";
import { migrationManager } from "./migrations";

let migrationsRunning = false;
let migrationsCompleted = false;

/**
 * Run pending migrations on startup
 * Can be called from Next.js initialization or API route
 *
 * @example
 * ```typescript
 * // In lib/mongodb.ts
 * await runMigrationsOnStartup();
 * ```
 */
export async function runMigrationsOnStartup(): Promise<void> {
  // Prevent concurrent migration runs
  if (migrationsRunning) {
    console.log("Migrations already running, skipping...");
    return;
  }

  if (migrationsCompleted && process.env.NODE_ENV === "production") {
    // Skip subsequent checks in production after first run
    return;
  }

  migrationsRunning = true;

  try {
    const db = mongoose.connection;

    // Only run if we have an active connection
    if (db.readyState !== 1) {
      console.log("Database not connected, skipping migrations");
      return;
    }

    console.log("🔄 Running database migrations...");
    await migrationManager.runPending(db);
    console.log("✓ Database migrations completed\n");

    migrationsCompleted = true;
  } catch (error) {
    console.error("❌ Migration error:", error);
    // In development, continue despite migration errors
    // In production, you might want to fail startup
    if (process.env.NODE_ENV === "production") {
      throw error;
    }
  } finally {
    migrationsRunning = false;
  }
}

/**
 * Check migration status
 * Useful for debugging and logging
 */
export async function checkMigrationStatus(): Promise<void> {
  try {
    const db = mongoose.connection;
    if (db.readyState === 1) {
      await migrationManager.showStatus(db);
    }
  } catch (error) {
    console.error("Error checking migration status:", error);
  }
}

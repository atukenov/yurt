#!/usr/bin/env node

/**
 * Database Migration CLI Tool
 * Run migrations, check status, and rollback changes
 *
 * Usage:
 *   npx ts-node scripts/migrate.ts run      - Run pending migrations
 *   npx ts-node scripts/migrate.ts status   - Show migration status
 *   npx ts-node scripts/migrate.ts rollback <version> - Rollback specific migration
 */

import mongoose from "mongoose";
import { migrationManager } from "../src/lib/migrations";

const MONGODB_URI = process.env.MONGODB_URI;

async function main() {
  if (!MONGODB_URI) {
    console.error("Error: MONGODB_URI environment variable not set");
    process.exit(1);
  }

  try {
    // Connect to MongoDB
    console.log("Connecting to MongoDB...");
    await mongoose.connect(MONGODB_URI);
    const db = mongoose.connection;

    const command = process.argv[2];

    if (command === "run") {
      console.log("Running pending migrations...\n");
      await migrationManager.runPending(db);
      console.log("\n✓ All migrations completed successfully");
    } else if (command === "status") {
      await migrationManager.showStatus(db);
    } else if (command === "rollback") {
      const version = process.argv[3];
      if (!version) {
        console.error("Error: Please specify a migration version to rollback");
        process.exit(1);
      }
      await migrationManager.rollback(db, version);
      console.log("✓ Rollback completed");
    } else {
      console.log("Database Migration Tool");
      console.log("=======================\n");
      console.log("Commands:");
      console.log("  run                   - Run all pending migrations");
      console.log("  status                - Show migration status");
      console.log("  rollback <version>    - Rollback a specific migration\n");
      console.log("Examples:");
      console.log("  npm run migrate run");
      console.log("  npm run migrate status");
      console.log("  npm run migrate rollback 001_add_user_indexes");
    }

    await db.close();
  } catch (error) {
    console.error("Migration error:", error);
    process.exit(1);
  }
}

main();

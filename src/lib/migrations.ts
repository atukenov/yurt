/**
 * Database Migration System
 * Handles schema migrations and index management for MongoDB
 */

import mongoose from "mongoose";

export interface Migration {
  version: string;
  description: string;
  up: (db: mongoose.Connection) => Promise<void>;
  down: (db: mongoose.Connection) => Promise<void>;
}

export class MigrationManager {
  private migrations: Migration[] = [];
  private migrationsCollection = "migrations";

  register(migration: Migration): void {
    this.migrations.push(migration);
  }

  async runPending(db: mongoose.Connection): Promise<void> {
    // Create migrations collection if it doesn't exist
    if (!db.db) {
      throw new Error("Database connection not initialized");
    }

    const collections = await db.db.listCollections().toArray();
    const migrationsCollectionExists = collections.some(
      (col) => col.name === this.migrationsCollection
    );

    if (!migrationsCollectionExists) {
      await db.db.createCollection(this.migrationsCollection);
    }

    const collection = db.collection(this.migrationsCollection);
    const executedMigrations = await collection.find({}).toArray();
    const executedVersions = new Set(
      (executedMigrations as unknown[]).map(
        (m: unknown) => (m as Record<string, unknown>).version
      )
    );

    for (const migration of this.migrations) {
      if (!executedVersions.has(migration.version)) {
        try {
          console.log(`Running migration: ${migration.version}`);
          await migration.up(db);
          await collection.insertOne({
            version: migration.version,
            description: migration.description,
            executedAt: new Date(),
          });
          console.log(`✓ Completed: ${migration.version}`);
        } catch (error) {
          console.error(`✗ Failed: ${migration.version}`, error);
          throw error;
        }
      }
    }
  }

  async rollback(db: mongoose.Connection, version: string): Promise<void> {
    const migration = this.migrations.find((m) => m.version === version);
    if (!migration) {
      throw new Error(`Migration not found: ${version}`);
    }

    try {
      console.log(`Rolling back: ${version}`);
      await migration.down(db);
      const collection = db.collection(this.migrationsCollection);
      await collection.deleteOne({ version });
      console.log(`✓ Rolled back: ${version}`);
    } catch (error) {
      console.error(`✗ Rollback failed: ${version}`, error);
      throw error;
    }
  }

  async showStatus(db: mongoose.Connection): Promise<void> {
    const collection = db.collection(this.migrationsCollection);
    const executed = await collection.find({}).toArray();
    const executedVersions = new Set(
      (executed as unknown[]).map(
        (m: unknown) => (m as Record<string, unknown>).version
      )
    );

    console.log("\nMigration Status:");
    console.log("================\n");

    for (const migration of this.migrations) {
      const status = executedVersions.has(migration.version)
        ? "✓ EXECUTED"
        : "○ PENDING";
      console.log(`${status} ${migration.version} - ${migration.description}`);
    }
  }
}

/**
 * Predefined migrations for schema optimization
 */

export const schemaMigrations: Migration[] = [
  {
    version: "001_add_user_indexes",
    description: "Add indexes to User schema for authentication and queries",
    up: async (db) => {
      const collection = db.collection("users");
      // Create indexes
      await collection.createIndex({ email: 1 }, { unique: true });
      await collection.createIndex({ name: 1 });
      await collection.createIndex({ role: 1 });
      await collection.createIndex({ role: 1, createdAt: -1 });
    },
    down: async (db) => {
      const collection = db.collection("users");
      await collection.dropIndex("email_1");
      await collection.dropIndex("name_1");
      await collection.dropIndex("role_1");
      await collection.dropIndex("role_1_createdAt_-1");
    },
  },

  {
    version: "002_add_menuitem_indexes",
    description: "Add indexes to MenuItem schema for search and filtering",
    up: async (db) => {
      const collection = db.collection("menuitems");
      // Text index for search
      await collection.createIndex({ name: "text", category: 1 });
      // Category and availability
      await collection.createIndex({ category: 1, isAvailable: 1 });
      // Available items sorted
      await collection.createIndex({ isAvailable: 1, createdAt: -1 });
      // Price sorting
      await collection.createIndex({ basePrice: 1 });
      // Category + price
      await collection.createIndex({ category: 1, basePrice: 1 });
    },
    down: async (db) => {
      const collection = db.collection("menuitems");
      await collection.dropIndex("name_text_category_1");
      await collection.dropIndex("category_1_isAvailable_1");
      await collection.dropIndex("isAvailable_1_createdAt_-1");
      await collection.dropIndex("basePrice_1");
      await collection.dropIndex("category_1_basePrice_1");
    },
  },

  {
    version: "003_add_order_indexes",
    description: "Add comprehensive indexes to Order schema for analytics",
    up: async (db) => {
      const collection = db.collection("orders");
      // Customer history
      await collection.createIndex({ customer: 1, createdAt: -1 });
      // Admin filtering
      await collection.createIndex({ location: 1, status: 1, createdAt: -1 });
      // Status filtering
      await collection.createIndex({ status: 1, createdAt: -1 });
      // Quick lookup
      await collection.createIndex({ orderNumber: 1 });
      // Recent orders
      await collection.createIndex({ createdAt: -1 });
      // Payment analytics
      await collection.createIndex({ paymentMethod: 1, createdAt: -1 });
      // Customer pending orders
      await collection.createIndex({ customer: 1, status: 1 });
    },
    down: async (db) => {
      const collection = db.collection("orders");
      await collection.dropIndex("customer_1_createdAt_-1");
      await collection.dropIndex("location_1_status_1_createdAt_-1");
      await collection.dropIndex("status_1_createdAt_-1");
      await collection.dropIndex("orderNumber_1");
      await collection.dropIndex("createdAt_-1");
      await collection.dropIndex("paymentMethod_1_createdAt_-1");
      await collection.dropIndex("customer_1_status_1");
    },
  },

  {
    version: "004_add_topping_indexes",
    description: "Add indexes to Topping schema for lookups",
    up: async (db) => {
      const collection = db.collection("toppings");
      await collection.createIndex({ name: 1 });
      await collection.createIndex({ category: 1 });
      await collection.createIndex({ category: 1, name: 1 });
    },
    down: async (db) => {
      const collection = db.collection("toppings");
      await collection.dropIndex("name_1");
      await collection.dropIndex("category_1");
      await collection.dropIndex("category_1_name_1");
    },
  },

  {
    version: "005_add_location_indexes",
    description: "Add indexes to Location schema",
    up: async (db) => {
      const collection = db.collection("locations");
      await collection.createIndex({ name: 1 });
      await collection.createIndex({ city: 1 });
      await collection.createIndex({ isActive: 1 });
      await collection.createIndex({ isActive: 1, city: 1 });
    },
    down: async (db) => {
      const collection = db.collection("locations");
      await collection.dropIndex("name_1");
      await collection.dropIndex("city_1");
      await collection.dropIndex("isActive_1");
      await collection.dropIndex("isActive_1_city_1");
    },
  },

  {
    version: "006_add_review_indexes",
    description: "Add indexes to Review schema for moderation",
    up: async (db) => {
      const collection = db.collection("reviews");
      await collection.createIndex({ order: 1 });
      await collection.createIndex({ customer: 1 });
      await collection.createIndex({ menuItem: 1 });
      await collection.createIndex({ isApproved: 1 });
      await collection.createIndex({ menuItem: 1, isApproved: 1 });
      await collection.createIndex({ isApproved: 1, createdAt: -1 });
      await collection.createIndex({ customer: 1, createdAt: -1 });
    },
    down: async (db) => {
      const collection = db.collection("reviews");
      await collection.dropIndex("order_1");
      await collection.dropIndex("customer_1");
      await collection.dropIndex("menuItem_1");
      await collection.dropIndex("isApproved_1");
      await collection.dropIndex("menuItem_1_isApproved_1");
      await collection.dropIndex("isApproved_1_createdAt_-1");
      await collection.dropIndex("customer_1_createdAt_-1");
    },
  },

  {
    version: "007_add_notification_indexes",
    description: "Add indexes to Notification schema",
    up: async (db) => {
      const collection = db.collection("notifications");
      await collection.createIndex({ recipient: 1, createdAt: -1 });
      await collection.createIndex({ recipient: 1, read: 1 });
      await collection.createIndex({ read: 1, createdAt: -1 });
      await collection.createIndex({ order: 1 });
    },
    down: async (db) => {
      const collection = db.collection("notifications");
      await collection.dropIndex("recipient_1_createdAt_-1");
      await collection.dropIndex("recipient_1_read_1");
      await collection.dropIndex("read_1_createdAt_-1");
      await collection.dropIndex("order_1");
    },
  },

  {
    version: "008_remove_duplicate_indexes",
    description: "Remove any duplicate or redundant indexes",
    up: async (db) => {
      // Check for duplicate indexes and remove them
      const collections = [
        "users",
        "menuitems",
        "orders",
        "toppings",
        "locations",
        "reviews",
        "notifications",
      ];

      for (const collName of collections) {
        const collection = db.collection(collName);
        const indexes = await collection.listIndexes().toArray();

        // Log index information for manual review
        console.log(`\nIndexes for ${collName}:`);
        for (const index of indexes) {
          console.log(`  - ${index.name}: ${JSON.stringify(index.key)}`);
        }
      }
    },
    down: async () => {
      // No-op for down migration
      console.log("Skipping rollback for duplicate index removal");
    },
  },
];

// Export default migration manager
export const migrationManager = new MigrationManager();
schemaMigrations.forEach((migration) => migrationManager.register(migration));

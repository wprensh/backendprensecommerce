DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core') THEN
        CREATE SCHEMA core;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS core.__ef_migrations_history (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'crm') THEN
            CREATE SCHEMA crm;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'catalog') THEN
            CREATE SCHEMA catalog;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'sales') THEN
            CREATE SCHEMA sales;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
            CREATE SCHEMA identity;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core') THEN
            CREATE SCHEMA core;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'support') THEN
            CREATE SCHEMA support;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE core.tenants (
        id uuid NOT NULL,
        slug character varying(63) NOT NULL,
        name character varying(200) NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT pk_tenants PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE crm.automation_rules (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        trigger character varying(30) NOT NULL,
        action character varying(30) NOT NULL,
        configuration jsonb NOT NULL,
        delay_minutes integer NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_automation_rules PRIMARY KEY (id),
        CONSTRAINT fk_automation_rules_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE catalog.categories (
        id uuid NOT NULL,
        name character varying(100) NOT NULL,
        slug character varying(100) NOT NULL,
        description character varying(1000),
        image_url character varying(1000),
        parent_id uuid,
        sort_order integer NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_categories PRIMARY KEY (id),
        CONSTRAINT fk_categories_categories_parent_id FOREIGN KEY (parent_id) REFERENCES catalog.categories (id) ON DELETE RESTRICT,
        CONSTRAINT fk_categories_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE identity.roles (
        id uuid NOT NULL,
        name character varying(100) NOT NULL,
        description character varying(500),
        permissions text[] NOT NULL,
        is_system boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_roles PRIMARY KEY (id),
        CONSTRAINT fk_roles_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE identity.users (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        email character varying(320) NOT NULL,
        password_hash text NOT NULL,
        phone character varying(30),
        is_active boolean NOT NULL,
        accepts_marketing boolean NOT NULL,
        tags text[] NOT NULL,
        last_login_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_users PRIMARY KEY (id),
        CONSTRAINT fk_users_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE crm.automation_runs (
        id uuid NOT NULL,
        rule_id uuid NOT NULL,
        status character varying(20) NOT NULL,
        payload jsonb NOT NULL,
        error character varying(2000),
        executed_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_automation_runs PRIMARY KEY (id),
        CONSTRAINT fk_automation_runs_automation_rules_rule_id FOREIGN KEY (rule_id) REFERENCES crm.automation_rules (id) ON DELETE CASCADE,
        CONSTRAINT fk_automation_runs_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE catalog.products (
        id uuid NOT NULL,
        sku character varying(64),
        name character varying(200) NOT NULL,
        description character varying(4000) NOT NULL,
        price numeric(12,2) NOT NULL,
        discount_percent integer NOT NULL,
        category_id uuid NOT NULL,
        image_url character varying(1000) NOT NULL,
        rating numeric(2,1) NOT NULL,
        review_count integer NOT NULL,
        stock integer NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_products PRIMARY KEY (id),
        CONSTRAINT ck_products_discount CHECK (discount_percent BETWEEN 0 AND 100),
        CONSTRAINT ck_products_price CHECK (price >= 0),
        CONSTRAINT ck_products_stock CHECK (stock >= 0),
        CONSTRAINT fk_products_categories_category_id FOREIGN KEY (category_id) REFERENCES catalog.categories (id) ON DELETE RESTRICT,
        CONSTRAINT fk_products_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE crm.customer_notes (
        id uuid NOT NULL,
        customer_id uuid NOT NULL,
        author_id uuid,
        body character varying(4000) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_customer_notes PRIMARY KEY (id),
        CONSTRAINT fk_customer_notes_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_customer_notes_users_author_id FOREIGN KEY (author_id) REFERENCES identity.users (id) ON DELETE SET NULL,
        CONSTRAINT fk_customer_notes_users_customer_id FOREIGN KEY (customer_id) REFERENCES identity.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE crm.loyalty_accounts (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        points_balance integer NOT NULL,
        tier character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_loyalty_accounts PRIMARY KEY (id),
        CONSTRAINT fk_loyalty_accounts_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_loyalty_accounts_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE sales.orders (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        status character varying(20) NOT NULL,
        payment_method character varying(20) NOT NULL,
        shipping_address_full_name character varying(200) NOT NULL,
        shipping_address_address character varying(500) NOT NULL,
        shipping_address_city character varying(100) NOT NULL,
        shipping_address_phone character varying(30) NOT NULL,
        total numeric(12,2) NOT NULL,
        currency character varying(3) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_orders PRIMARY KEY (id),
        CONSTRAINT fk_orders_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_orders_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE identity.user_roles (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        role_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_user_roles PRIMARY KEY (id),
        CONSTRAINT fk_user_roles_roles_role_id FOREIGN KEY (role_id) REFERENCES identity.roles (id) ON DELETE CASCADE,
        CONSTRAINT fk_user_roles_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_user_roles_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE crm.loyalty_transactions (
        id uuid NOT NULL,
        account_id uuid NOT NULL,
        type character varying(20) NOT NULL,
        points integer NOT NULL,
        reason character varying(200) NOT NULL,
        order_id uuid,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_loyalty_transactions PRIMARY KEY (id),
        CONSTRAINT fk_loyalty_transactions_loyalty_accounts_account_id FOREIGN KEY (account_id) REFERENCES crm.loyalty_accounts (id) ON DELETE CASCADE,
        CONSTRAINT fk_loyalty_transactions_orders_order_id FOREIGN KEY (order_id) REFERENCES sales.orders (id) ON DELETE SET NULL,
        CONSTRAINT fk_loyalty_transactions_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE sales.order_items (
        id uuid NOT NULL,
        order_id uuid NOT NULL,
        product_id uuid NOT NULL,
        product_name character varying(200) NOT NULL,
        unit_price numeric(12,2) NOT NULL,
        quantity integer NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_order_items PRIMARY KEY (id),
        CONSTRAINT fk_order_items_orders_order_id FOREIGN KEY (order_id) REFERENCES sales.orders (id) ON DELETE CASCADE,
        CONSTRAINT fk_order_items_products_product_id FOREIGN KEY (product_id) REFERENCES catalog.products (id) ON DELETE RESTRICT,
        CONSTRAINT fk_order_items_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE sales.payments (
        id uuid NOT NULL,
        order_id uuid NOT NULL,
        method character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        amount numeric(12,2) NOT NULL,
        currency character varying(3) NOT NULL,
        provider character varying(50) NOT NULL,
        provider_reference character varying(200),
        failure_reason character varying(500),
        processed_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_payments PRIMARY KEY (id),
        CONSTRAINT fk_payments_orders_order_id FOREIGN KEY (order_id) REFERENCES sales.orders (id) ON DELETE CASCADE,
        CONSTRAINT fk_payments_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE support.tickets (
        id uuid NOT NULL,
        customer_id uuid,
        contact_email character varying(320),
        order_id uuid,
        subject character varying(200) NOT NULL,
        status character varying(20) NOT NULL,
        priority character varying(20) NOT NULL,
        channel character varying(20) NOT NULL,
        assigned_to_id uuid,
        closed_at timestamp with time zone,
        satisfaction_score integer,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_tickets PRIMARY KEY (id),
        CONSTRAINT ck_tickets_satisfaction CHECK (satisfaction_score IS NULL OR satisfaction_score BETWEEN 1 AND 5),
        CONSTRAINT fk_tickets_orders_order_id FOREIGN KEY (order_id) REFERENCES sales.orders (id) ON DELETE SET NULL,
        CONSTRAINT fk_tickets_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_tickets_users_assigned_to_id FOREIGN KEY (assigned_to_id) REFERENCES identity.users (id) ON DELETE SET NULL,
        CONSTRAINT fk_tickets_users_customer_id FOREIGN KEY (customer_id) REFERENCES identity.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE TABLE support.ticket_messages (
        id uuid NOT NULL,
        ticket_id uuid NOT NULL,
        author_id uuid,
        body character varying(8000) NOT NULL,
        is_internal boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_ticket_messages PRIMARY KEY (id),
        CONSTRAINT fk_ticket_messages_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_ticket_messages_tickets_ticket_id FOREIGN KEY (ticket_id) REFERENCES support.tickets (id) ON DELETE CASCADE,
        CONSTRAINT fk_ticket_messages_users_author_id FOREIGN KEY (author_id) REFERENCES identity.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_automation_rules_tenant_id ON crm.automation_rules (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_automation_rules_tenant_id_trigger_is_active ON crm.automation_rules (tenant_id, trigger, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_automation_runs_rule_id ON crm.automation_runs (rule_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_automation_runs_tenant_id ON crm.automation_runs (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_automation_runs_tenant_id_status ON crm.automation_runs (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_categories_parent_id ON catalog.categories (parent_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_categories_tenant_id ON catalog.categories (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_categories_tenant_id_slug ON catalog.categories (tenant_id, slug);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_customer_notes_author_id ON crm.customer_notes (author_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_customer_notes_customer_id ON crm.customer_notes (customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_customer_notes_tenant_id ON crm.customer_notes (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_loyalty_accounts_tenant_id ON crm.loyalty_accounts (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_loyalty_accounts_tenant_id_user_id ON crm.loyalty_accounts (tenant_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_loyalty_accounts_user_id ON crm.loyalty_accounts (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_loyalty_transactions_account_id ON crm.loyalty_transactions (account_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_loyalty_transactions_order_id ON crm.loyalty_transactions (order_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_loyalty_transactions_tenant_id ON crm.loyalty_transactions (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_order_items_order_id ON sales.order_items (order_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_order_items_product_id ON sales.order_items (product_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_order_items_tenant_id ON sales.order_items (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_orders_tenant_id ON sales.orders (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_orders_tenant_id_status ON sales.orders (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_orders_tenant_id_user_id ON sales.orders (tenant_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_orders_user_id ON sales.orders (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_payments_order_id ON sales.payments (order_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_payments_provider_provider_reference ON sales.payments (provider, provider_reference);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_payments_tenant_id ON sales.payments (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_products_category_id ON catalog.products (category_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_products_tenant_id ON catalog.products (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_products_tenant_id_category_id ON catalog.products (tenant_id, category_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_products_tenant_id_sku ON catalog.products (tenant_id, sku) WHERE sku IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_roles_tenant_id ON identity.roles (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_roles_tenant_id_name ON identity.roles (tenant_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_tenants_slug ON core.tenants (slug);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_ticket_messages_author_id ON support.ticket_messages (author_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_ticket_messages_tenant_id ON support.ticket_messages (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_ticket_messages_ticket_id ON support.ticket_messages (ticket_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_tickets_assigned_to_id ON support.tickets (assigned_to_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_tickets_customer_id ON support.tickets (customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_tickets_order_id ON support.tickets (order_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_tickets_tenant_id ON support.tickets (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_tickets_tenant_id_status ON support.tickets (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_user_roles_role_id ON identity.user_roles (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_user_roles_tenant_id ON identity.user_roles (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_user_roles_tenant_id_user_id_role_id ON identity.user_roles (tenant_id, user_id, role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_user_roles_user_id ON identity.user_roles (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_users_tags ON identity.users USING gin (tags);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE INDEX ix_users_tenant_id ON identity.users (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_users_tenant_id_email ON identity.users (tenant_id, email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102180628_InitialCreate') THEN
    INSERT INTO core.__ef_migrations_history (migration_id, product_version)
    VALUES ('20261102180628_InitialCreate', '9.0.22');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102181015_AddOrderNumberAndCheckoutTotals') THEN
    ALTER TABLE sales.orders ADD order_number character varying(32) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102181015_AddOrderNumberAndCheckoutTotals') THEN
    ALTER TABLE sales.orders ADD shipping_cost numeric(12,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102181015_AddOrderNumberAndCheckoutTotals') THEN
    ALTER TABLE sales.orders ADD subtotal numeric(12,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102181015_AddOrderNumberAndCheckoutTotals') THEN
    UPDATE sales.orders
    SET order_number = 'PR-' || to_char(created_at, 'YYYYMMDD') || '-' || upper(substr(replace(id::text, '-', ''), 25, 8)),
        subtotal = total
    WHERE order_number = '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102181015_AddOrderNumberAndCheckoutTotals') THEN
    CREATE UNIQUE INDEX ix_orders_tenant_id_order_number ON sales.orders (tenant_id, order_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102181015_AddOrderNumberAndCheckoutTotals') THEN
    INSERT INTO core.__ef_migrations_history (migration_id, product_version)
    VALUES ('20261102181015_AddOrderNumberAndCheckoutTotals', '9.0.22');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    DROP INDEX crm.ix_loyalty_transactions_account_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    ALTER TABLE support.tickets ADD category character varying(20) NOT NULL DEFAULT 'Other';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    ALTER TABLE support.tickets ADD number integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    UPDATE support.tickets t SET number = s.rn
    FROM (SELECT id, ROW_NUMBER() OVER (PARTITION BY tenant_id ORDER BY created_at) AS rn
          FROM support.tickets) s
    WHERE t.id = s.id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    ALTER TABLE support.tickets ADD satisfaction_comment character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    ALTER TABLE sales.orders ADD loyalty_discount numeric(12,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    ALTER TABLE crm.loyalty_accounts ADD lifetime_points integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    UPDATE crm.loyalty_accounts SET lifetime_points = GREATEST(points_balance, 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE TABLE crm.loyalty_vouchers (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        code character varying(20) NOT NULL,
        amount numeric(12,2) NOT NULL,
        points integer NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        redeemed_at timestamp with time zone,
        order_id uuid,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_loyalty_vouchers PRIMARY KEY (id),
        CONSTRAINT fk_loyalty_vouchers_orders_order_id FOREIGN KEY (order_id) REFERENCES sales.orders (id) ON DELETE SET NULL,
        CONSTRAINT fk_loyalty_vouchers_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_loyalty_vouchers_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE INDEX ix_tickets_tenant_id_customer_id ON support.tickets (tenant_id, customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE UNIQUE INDEX ix_tickets_tenant_id_number ON support.tickets (tenant_id, number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE INDEX ix_loyalty_transactions_account_id_created_at ON crm.loyalty_transactions (account_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE INDEX ix_loyalty_vouchers_order_id ON crm.loyalty_vouchers (order_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE INDEX ix_loyalty_vouchers_tenant_id ON crm.loyalty_vouchers (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE UNIQUE INDEX ix_loyalty_vouchers_tenant_id_code ON crm.loyalty_vouchers (tenant_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE INDEX ix_loyalty_vouchers_tenant_id_user_id ON crm.loyalty_vouchers (tenant_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    CREATE INDEX ix_loyalty_vouchers_user_id ON crm.loyalty_vouchers (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102200626_SupportAndLoyalty') THEN
    INSERT INTO core.__ef_migrations_history (migration_id, product_version)
    VALUES ('20261102200626_SupportAndLoyalty', '9.0.22');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    DROP INDEX crm.ix_automation_runs_tenant_id_status;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    ALTER TABLE crm.automation_runs ADD dedup_key character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    ALTER TABLE crm.automation_runs ADD scheduled_at timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    ALTER TABLE crm.automation_runs ADD user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE TABLE crm.cart_activities (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        item_count integer NOT NULL,
        last_activity_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_cart_activities PRIMARY KEY (id),
        CONSTRAINT fk_cart_activities_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_cart_activities_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE TABLE crm.customer_events (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        type character varying(30) NOT NULL,
        reference_id uuid,
        amount numeric(12,2),
        occurred_at timestamp with time zone NOT NULL,
        processed_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        CONSTRAINT pk_customer_events PRIMARY KEY (id),
        CONSTRAINT fk_customer_events_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_customer_events_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE TABLE crm.customer_segments (
        id uuid NOT NULL,
        name character varying(120) NOT NULL,
        description character varying(500) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        tenant_id uuid NOT NULL,
        rules jsonb NOT NULL,
        CONSTRAINT pk_customer_segments PRIMARY KEY (id),
        CONSTRAINT fk_customer_segments_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES core.tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE UNIQUE INDEX ix_automation_runs_tenant_id_dedup_key ON crm.automation_runs (tenant_id, dedup_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_automation_runs_tenant_id_status_scheduled_at ON crm.automation_runs (tenant_id, status, scheduled_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_automation_runs_tenant_id_user_id ON crm.automation_runs (tenant_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_automation_runs_user_id ON crm.automation_runs (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_cart_activities_tenant_id ON crm.cart_activities (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_cart_activities_tenant_id_item_count_last_activity_at ON crm.cart_activities (tenant_id, item_count, last_activity_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE UNIQUE INDEX ix_cart_activities_tenant_id_user_id ON crm.cart_activities (tenant_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE UNIQUE INDEX ix_cart_activities_user_id ON crm.cart_activities (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_customer_events_tenant_id ON crm.customer_events (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_customer_events_tenant_id_processed_at ON crm.customer_events (tenant_id, processed_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_customer_events_tenant_id_user_id_occurred_at ON crm.customer_events (tenant_id, user_id, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_customer_events_user_id ON crm.customer_events (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE INDEX ix_customer_segments_tenant_id ON crm.customer_segments (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    CREATE UNIQUE INDEX ix_customer_segments_tenant_id_name ON crm.customer_segments (tenant_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    ALTER TABLE crm.automation_runs ADD CONSTRAINT fk_automation_runs_users_user_id FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core.__ef_migrations_history WHERE "migration_id" = '20261102202115_CrmAutomations') THEN
    INSERT INTO core.__ef_migrations_history (migration_id, product_version)
    VALUES ('20261102202115_CrmAutomations', '9.0.22');
    END IF;
END $EF$;
COMMIT;


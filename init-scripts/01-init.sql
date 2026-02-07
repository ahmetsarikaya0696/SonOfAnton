-- SonOfAnton PostgreSQL Initialization Script
-- Bu script container ilk olusturuldugunda otomatik calisir.

-- pgvector extension'i olustur
CREATE EXTENSION IF NOT EXISTS vector;

-- Kullaniciya superuser yetkileri ver (pgvector icin gerekli)
ALTER USER sonofanton WITH SUPERUSER;

-- Veritabani ayarlari
ALTER DATABASE sonofanton_db OWNER TO sonofanton;

-- Baglanti izinleri
GRANT ALL PRIVILEGES ON DATABASE sonofanton_db TO sonofanton;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO sonofanton;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO sonofanton;

-- Gelecekte olusturulacak tablolar icin varsayilan yetkiler
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO sonofanton;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO sonofanton;

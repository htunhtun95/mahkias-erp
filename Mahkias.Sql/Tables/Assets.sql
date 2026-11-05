CREATE TABLE [media].[assets]
(
    [id] UNIQUEIDENTIFIER CONSTRAINT [DF_media_assets_id] DEFAULT (NEWSEQUENTIALID()) NOT NULL,
    [file_url] NVARCHAR(1000) NOT NULL,
    [file_name] NVARCHAR(260) NOT NULL,
    [file_type] NVARCHAR(100) NOT NULL,
    [file_size] BIGINT CONSTRAINT [DF_media_assets_file_size] DEFAULT (0) NOT NULL,
    [sort_order] INT CONSTRAINT [DF_media_assets_sort_order] DEFAULT (0) NOT NULL,
    [created_at] DATETIME2 CONSTRAINT [DF_media_assets_created_at] DEFAULT (SYSUTCDATETIME()) NOT NULL,
    [drive_file_id] NVARCHAR(128) NULL,
    CONSTRAINT [PK_media_assets] PRIMARY KEY CLUSTERED ([id] ASC)
);

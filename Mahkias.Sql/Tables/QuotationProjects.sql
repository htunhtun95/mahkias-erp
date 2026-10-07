CREATE TABLE [projects].[QuotationProjects]
(
    [QuotationId] INT NOT NULL,
    [ProjectId] INT NOT NULL,
    CONSTRAINT [PK_QuotationProjects] PRIMARY KEY CLUSTERED ([QuotationId] ASC, [ProjectId] ASC),
    CONSTRAINT [FK_QuotationProjects_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_QuotationProjects_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE CASCADE
);

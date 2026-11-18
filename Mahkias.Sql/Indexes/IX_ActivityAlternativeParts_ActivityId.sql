CREATE NONCLUSTERED INDEX [IX_ActivityAlternativeParts_ActivityId]
    ON [projects].[ActivityAlternativeParts]([ActivityId] ASC)
    INCLUDE([MainPartNo], [AlternativePartNo]);

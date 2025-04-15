CREATE OR ALTER PROCEDURE Internal_PurchaseRequest_AdminReviewRequests
(
	@ids VARCHAR(MAX),
	@reviewStatus INT
)
AS
BEGIN
    UPDATE dbo.PurchaseRequest
	SET ReviewStatus = @reviewStatus, UpdatedDate = DATEADD(HOUR, 7, GETUTCDATE())
	WHERE Id IN (SELECT value FROM STRING_SPLIT(@ids, ','))
END
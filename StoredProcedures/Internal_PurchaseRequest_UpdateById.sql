CREATE OR ALTER PROCEDURE Internal_PurchaseRequest_UpdateById
(
	@id INT,
	@reviewNote NVARCHAR(MAX),
	@reviewStatus INT
)
AS
BEGIN
	DECLARE @directorRejected INT = 4, @directorUpdateRequest INT = 8,
			@accountantReject INT = 3, @accountantUpdateRequest INT = 7;

	IF @reviewStatus = @accountantUpdateRequest
	BEGIN
	    UPDATE dbo.PurchaseRequest
		SET SecondHrComment = @reviewNote
		WHERE Id = @id
	END
	ELSE IF @reviewStatus = @accountantReject
	BEGIN
	    UPDATE dbo.PurchaseRequest
		SET RejectReason = @reviewNote
		WHERE Id = @id
	END
    ELSE IF @reviewStatus = @directorUpdateRequest
	BEGIN
		UPDATE dbo.PurchaseRequest
		SET DirectorComment = @reviewNote
		WHERE Id = @id
	END
	ELSE IF @reviewStatus = @directorRejected
	BEGIN
	    UPDATE dbo.PurchaseRequest
		SET RejectReason = @reviewNote
		WHERE Id = @id
	END

	UPDATE dbo.PurchaseRequest
	SET ReviewStatus = @reviewStatus
	WHERE Id = @id
END
-- Create the main system database if it does not exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'StockerManagementDB')
BEGIN
    CREATE DATABASE StockerManagementDB;
END
GO

USE StockerManagementDB;
GO

-- 1.1 Stockers Table
CREATE TABLE Stockers (
    StockerID VARCHAR(10) NOT NULL,
    StockerName NVARCHAR(50) NOT NULL,
    [Status] VARCHAR(10) NOT NULL,
    ConnectionStatus VARCHAR(10) NOT NULL,
    OperationState VARCHAR(20) NOT NULL,
    alarms NVARCHAR(MAX) NULL,
    LastHeartbeat DATETIME NOT NULL DEFAULT GETDATE(),
    
    CONSTRAINT PK_Stockers PRIMARY KEY (StockerID),
    CONSTRAINT CHK_Stockers_Status CHECK ([Status] IN ('Active', 'Reserved')),
    CONSTRAINT CHK_Stockers_Connection CHECK (ConnectionStatus IN ('ONLINE', 'OFFLINE')),
    CONSTRAINT CHK_Stockers_Operation CHECK (OperationState IN ('IDLE', 'TRAVELING')),
    CONSTRAINT CHK_Stockers_AlarmsJson CHECK (alarms IS NULL OR ISJSON(alarms) > 0)
);

-- 1.2 Shelves Table (Composite Primary Key)
CREATE TABLE Shelves (
    StockerID VARCHAR(10) NOT NULL,
    ShelfName VARCHAR(10) NOT NULL,
    CarrierID VARCHAR(20) NULL,
    InTime DATETIME NULL,
    
    CONSTRAINT PK_Shelves PRIMARY KEY (StockerID, ShelfName),
    CONSTRAINT FK_Shelves_Stockers FOREIGN KEY (StockerID) 
        REFERENCES Stockers(StockerID) ON DELETE CASCADE
);

-- 1.3 Jobs Table
CREATE TABLE Jobs (
    JobID VARCHAR(20) NOT NULL,
    StockerID VARCHAR(10) NOT NULL,
    CarrierID VARCHAR(20) NOT NULL,
    SourceLocation VARCHAR(10) NOT NULL,
    DestLocation VARCHAR(10) NOT NULL,
    JobStatus VARCHAR(15) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    ClosedAt DATETIME NULL, -- Allowed null because open/pending jobs are not closed yet
    
    CONSTRAINT PK_Jobs PRIMARY KEY (JobID),
    CONSTRAINT FK_Jobs_Stockers FOREIGN KEY (StockerID) 
        REFERENCES Stockers(StockerID),
    CONSTRAINT CHK_Jobs_Status CHECK (JobStatus IN ('PENDING', 'RUNNING', 'COMPLETED', 'ABORTED'))
);

-- 1.4 Logs Table
CREATE TABLE Logs (
    LogID BIGINT IDENTITY(1,1) NOT NULL,
    [Timestamp] DATETIME NOT NULL DEFAULT GETDATE(),
    StockerID VARCHAR(10) NOT NULL,
    [Level] VARCHAR(10) NOT NULL,
    [Message] NVARCHAR(255) NOT NULL,
    
    CONSTRAINT PK_Logs PRIMARY KEY (LogID),
    CONSTRAINT CHK_Logs_Level CHECK ([Level] IN ('INFO', 'WARN', 'ALARM'))
);
GO



    ----------------------------------------------------
    -- 1. INSERT DATA INTO TABLE
    ----------------------------------------------------
    INSERT INTO Stockers (StockerID, StockerName, [Status], ConnectionStatus, OperationState, alarms, LastHeartbeat)
    VALUES 
    (
        'STK001', 'STK1', 'Active', 'ONLINE', 'IDLE', 
        '[{"errorCode": "ERR-003", "message": "出庫時ソース空異常"}]', 
        GETDATE()
    ),
    (
        'STK002', 'STK2', 'Reserved', 'OFFLINE', 'IDLE', -- Shifted UNKNOWN to IDLE to align with schema constraint rules
        '[{"errorCode": "ERR-003", "message": "出庫時ソース空異常"}]', 
        GETDATE()
    ),
    (
        'STK003', 'STK3', 'Active', 'ONLINE', 'TRAVELING', 
        '[]', 
        GETDATE()
    ),
    (
        'STK004', 'STK4', 'Active', 'ONLINE', 'IDLE', 
        '[]', 
        GETDATE()
    );

    ----------------------------------------------------
    -- 2. SEED SHELVES TABLE
    ----------------------------------------------------
    -- Data Note: Because (StockerID, ShelfName) is your Primary Key, duplicate combinations 
    -- like 'STK004 + OUT_PORT' have been given individual sequential suffix names (e.g., OUT_PORT_1, OUT_PORT_2) 
    -- to meet relational uniqueness standards.
    INSERT INTO Shelves (StockerID, ShelfName, CarrierID, InTime)
    VALUES
    -- STK001 Shelves
    ('STK001', 'IN_PORT',   NULL,       NULL),
    ('STK001', 'SHELF_A1',  'CST-1001', '2026-06-16 10:00:00'),
    ('STK001', 'SHELF_A2',  NULL,       NULL),
    ('STK001', 'SHELF_A3',  NULL,       NULL),
    ('STK001', 'OUT_PORT',  'CST-1005', '2026-06-16 11:20:00'),

    -- STK004 Shelves
    ('STK004', 'IN_PORT',   NULL,        NULL),
    ('STK004', 'SHELF_A1',  'CST-1004',  '2026-06-16 10:00:00'),
    ('STK004', 'SHELF_A11', 'CST-1005',  '2026-06-16 10:00:00'),
    ('STK004', 'SHELF_A2',  NULL,        NULL),
    ('STK004', 'SHELF_A12', 'CST-10012', '2026-06-17 10:00:00'),
    ('STK004', 'SHELF_A3',  NULL,        NULL),
    ('STK004', 'SHELF_A4',  NULL,        NULL),
    ('STK004', 'SHELF_A9',  NULL,        NULL),
    ('STK004', 'SHELF_A10', NULL,        NULL),
    ('STK004', 'OUT_PORT',  'CST-1006',  '2026-06-16 11:20:00'),
    ('STK004', 'OUT_PORT2', 'CST-1008',  '2026-06-16 11:20:00'), -- Suffix adjusted for key uniformity
    ('STK004', 'OUT_PORT3', 'CST-1015',  '2026-06-16 11:20:00'), -- Suffix adjusted for key uniformity
    ('STK004', 'SHELF_A5',  'CST-1007',  '2026-06-16 11:20:00'),

    -- STK003 Shelves
    ('STK003', 'IN_PORT',   NULL,       NULL),
    ('STK003', 'SHELF_B1',  'CST-2002', '2026-06-22 01:00:00'),
    ('STK003', 'SHELF_B2',  NULL,       NULL),
    ('STK003', 'OUT_PORT',  NULL,       NULL);

    ----------------------------------------------------
    -- 3. SEED JOBS TABLE
    ----------------------------------------------------
    INSERT INTO Jobs (JobID, StockerID, CarrierID, SourceLocation, DestLocation, JobStatus, CreatedAt)
    VALUES 
    ('JOB1001', 'STK001', 'CST-1001', 'IN_PORT', 'SHELF_A1', 'RUNNING', '2026-06-23 12:00:00'),
    ('JOB1002', 'STK003', 'CST-2002', 'IN_PORT', 'SHELF_B1', 'PENDING', '2026-06-23 13:00:00');

    ----------------------------------------------------
    -- 4. SEED LOGS TABLE
    ----------------------------------------------------
    INSERT INTO Logs ([Timestamp], [Level], [Message], StockerID)
    VALUES 
    ('2026-06-22 05:15:00', 'INFO',  N'搬送動作完了 (TransferCompleted)', 'STK001'),
    ('2026-06-22 04:05:00', 'ALARM', N'出庫時ソース空異常 (ERR-003)',       'STK002'),
    ('2026-06-22 03:40:00', 'WARN',  N'通信遅延を検出しました',             'STK003'),
    ('2026-06-21 23:00:00', 'INFO',  N'ストッカー起動シーケンス完了',       'STK001');

    -- Commit if everything runs flawlessly
    COMMIT TRANSACTION;
    PRINT 'Database seeded successfully with all mock items!';


    -- Display Table with data 
    select * from Stockers;
    select * from Shelves;
    select * from Jobs;
    select * from Logs;
   

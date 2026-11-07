// 仅适用于 map-audit-init.csx 的矩形试验图与当前正交相机。
if (DayScene.SceneManager.Instance.CurrentActiveMapLabel != MapAudit.Label)
    throw new InvalidOperationException("必须先进入本次试验地图");
var auditVcam = DayScene.SceneManager.Instance.virtualCamera;
var auditConfiner = auditVcam.GetComponent<Cinemachine.CinemachineConfiner>();
var auditOffsetValue = auditVcam.GetComponent<CinemachineCameraOffset>().m_Offset;
var auditViewHalfY = Camera.main.orthographicSize;
var auditViewHalfX = auditViewHalfY * Camera.main.aspect;
var auditMargin = 1f / 32f;
var auditMinX = -18 + auditViewHalfX - auditOffsetValue.x + auditMargin;
var auditMaxX = 18 - auditViewHalfX - auditOffsetValue.x - auditMargin;
var auditMinY = -10 + auditViewHalfY - auditOffsetValue.y + auditMargin;
var auditMaxY = 10 - auditViewHalfY - auditOffsetValue.y - auditMargin;
DayScene.SceneManager.Instance.CurrentActiveMap.boundingShape.Cast<PolygonCollider2D>().SetPath(0,
    new Vector2[] { new(auditMinX, auditMinY), new(auditMinX, auditMaxY), new(auditMaxX, auditMaxY), new(auditMaxX, auditMinY) });
auditConfiner.InvalidatePathCache();
$"centerBounds=({auditMinX},{auditMinY})..({auditMaxX},{auditMaxY})"

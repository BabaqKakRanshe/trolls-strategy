-- Player events for tools/stats/players.py.
-- Unity Dashboard -> Analytics -> SQL Data Explorer (environment production): paste, Run, Export (CSV),
-- put the file into unity/TrollStategy/Builds/Stats/players/exports and run python tools/stats/players.py.
-- Keep it to these four columns: the script reads every field (session, version, quest, battle, colony) from
-- EVENT_JSON, and a wider select makes the explorer hang. Exports may overlap: an event counts once.
SELECT EVENT_TIMESTAMP, EVENT_NAME, USER_ID, EVENT_JSON
FROM EVENTS
WHERE EVENT_DATE >= CURRENT_DATE - 30
  AND EVENT_NAME IN ('questStarted', 'questCompleted', 'campaignCompleted', 'battleFinished', 'progressHeartbeat')
ORDER BY EVENT_TIMESTAMP

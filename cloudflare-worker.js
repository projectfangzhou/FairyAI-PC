// Cloudflare Worker for FairyAI PC-Mobile relay
// Deploy this to Cloudflare Workers for P-M sync when devices aren't on the same LAN.
//
// Setup:
// 1. Go to Cloudflare Dashboard > Workers & Pages
// 2. Create a new Worker, paste this code
// 3. Note the worker URL (e.g. https://fairyai-sync.your-subdomain.workers.dev)
// 4. Enter the URL in FairyAI installer "中转网站" field

export default {
  async fetch(request, env) {
    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type',
    };

    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: corsHeaders });
    }

    const url = new URL(request.url);

    // POST /sync — receive data from source device
    if (request.method === 'POST' && url.pathname === '/sync') {
      try {
        const data = await request.json();
        // Store in KV (if bound) or in-memory (for testing)
        const key = `sync_${data.target_device}_${Date.now()}`;
        if (env.FAIRYAI_KV) {
          await env.FAIRYAI_KV.put(key, JSON.stringify(data), {
            expirationTtl: 86400 // 24h TTL
          });
        }
        return new Response(JSON.stringify({
          status: 'ok',
          key: key,
          message: 'Data queued for delivery'
        }), {
          headers: { ...corsHeaders, 'Content-Type': 'application/json' }
        });
      } catch (e) {
        return new Response(JSON.stringify({ error: e.message }), {
          status: 400,
          headers: { ...corsHeaders, 'Content-Type': 'application/json' }
        });
      }
    }

    // GET /poll/:deviceId — check for pending data
    if (request.method === 'GET' && url.pathname.startsWith('/poll/')) {
      const deviceId = url.pathname.split('/')[2];
      if (env.FAIRYAI_KV) {
        const list = await env.FAIRYAI_KV.list({ prefix: `sync_${deviceId}_` });
        const items = [];
        for (const key of list.keys) {
          const value = await env.FAIRYAI_KV.get(key.name);
          if (value) items.push(JSON.parse(value));
          await env.FAIRYAI_KV.delete(key.name);
        }
        return new Response(JSON.stringify({ items }), {
          headers: { ...corsHeaders, 'Content-Type': 'application/json' }
        });
      }
      return new Response(JSON.stringify({ items: [] }), {
        headers: { ...corsHeaders, 'Content-Type': 'application/json' }
      });
    }

    // GET /health — health check
    return new Response(JSON.stringify({
      status: 'ok',
      service: 'FairyAI Relay',
      timestamp: new Date().toISOString()
    }), {
      headers: { ...corsHeaders, 'Content-Type': 'application/json' }
    });
  }
};

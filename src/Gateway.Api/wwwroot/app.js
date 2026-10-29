const $ = id => document.getElementById(id);
const money = value => value == null ? 'No disponible' : new Intl.NumberFormat('es-MX', {style:'currency',currency:'MXN'}).format(value);
let payload = null;
const form = $('query-form');
form.addEventListener('submit', async event => {
  event.preventDefault();
  const id = $('product-id').value;
  const path = `/api/v1/products/${encodeURIComponent(id)}/summary`;
  $('endpoint').textContent = `GET ${path}`;
  $('query-button').disabled = true;
  document.querySelector('main').setAttribute('aria-busy','true');
  $('alert').hidden = true;
  $('product-content').hidden = true;
  $('summary-status').textContent = 'Consultando las tres fuentes…';
  document.querySelectorAll('.source-state').forEach(el => { el.textContent='Consultando…'; el.classList.remove('error'); });
  document.querySelectorAll('.duration').forEach(el=>el.textContent='—');
  payload = null;
  $('json-output').textContent = '';
  const start = performance.now();
  try {
    const response = await fetch(path, {signal:AbortSignal.timeout(35000)});
    const data = await response.json();
    payload = data;
    $('json-output').textContent = JSON.stringify(data,null,2);
    $('request-time').textContent = `HTTP ${response.status} · ${Math.round(performance.now()-start)} ms · ${new Date().toLocaleTimeString('es-MX')}`;
    if (!response.ok) throw new Error(data.detail || data.title || 'No se pudo consultar el producto.');
    $('product-content').hidden = false;
    for (const [element,value] of Object.entries({title:data.title,description:data.description,category:data.category,'art-id':data.product_id,'price-mxn':money(data.price_mxn),'price-usd':money(data.price_usd),units:data.available_units ?? 'No disponible',warehouse:data.warehouse ?? 'No disponible',delivery:data.estimated_delivery_days == null ? 'No disponible' : `${data.estimated_delivery_days} días hábiles`})) $(element).textContent = value;
    $('device').className = `device ${id==='P-102'?'audio':id==='P-103'?'keys':'laptop'}`;
    $('stock-badge').textContent = {IN_STOCK:'● En stock',OUT_OF_STOCK:'Sin existencias',UNKNOWN:'Stock no disponible'}[data.stock_status];
    $('stock-badge').classList.toggle('warn',data.stock_status!=='IN_STOCK');
    $('rate-note').textContent = data.exchange_rate == null ? 'Tipo de cambio no disponible. El precio base en USD sigue vigente.' : `1 USD = ${data.exchange_rate} MXN · Frankfurter · Tasa de referencia del ${data.exchange_rate_date}. Actualización diaria.`;
    $('summary-status').textContent = data.status==='SUCCESS' ? '● Todas las fuentes disponibles' : '● Respuesta parcial';
    for (const service of data.services) {
      const card = document.querySelector(`[data-service="${service.service}"]`);
      if (!card) continue;
      const state = card.querySelector('.source-state');
      state.textContent = service.status==='SUCCESS' ? '● Disponible' : `● ${service.error_code}`;
      state.classList.toggle('error',service.status!=='SUCCESS');
      card.querySelector('.duration').textContent = `${service.duration_ms} ms`;
    }
    if(data.status==='PARTIAL') { $('alert').hidden=false; $('alert').className='alert'; $('alert').textContent='Algunas fuentes no están disponibles. Se muestran los datos confirmados; vuelve a consultar para reintentar.'; }
  } catch(error) {
    $('alert').hidden=false; $('alert').className='alert error'; $('alert').textContent=error.name==='TimeoutError'?'La consulta excedió el tiempo de espera. Intenta nuevamente.':error.message;
    $('summary-status').textContent='● Consulta sin completar';
    document.querySelectorAll('.source-state').forEach(el=>{el.textContent='Sin resultado consolidado';el.classList.add('error');});
    if(!payload) $('request-time').textContent=`Error de conexión · ${Math.round(performance.now()-start)} ms`;
  } finally { $('query-button').disabled=false; document.querySelector('main').setAttribute('aria-busy','false'); }
});
$('toggle-json').addEventListener('click',()=>{const show=$('json-panel').hidden;$('json-panel').hidden=!show;$('toggle-json').setAttribute('aria-expanded',String(show));$('toggle-json').textContent=show?'Ocultar JSON { }':'Ver JSON { }';});
$('copy-json').addEventListener('click',async()=>{if(!payload)return;try{await navigator.clipboard.writeText(JSON.stringify(payload,null,2));$('copy-json').textContent='Copiado';}catch{$('copy-json').textContent='Selecciona el texto para copiar';}setTimeout(()=>$('copy-json').textContent='Copiar JSON',2000);});
form.requestSubmit();

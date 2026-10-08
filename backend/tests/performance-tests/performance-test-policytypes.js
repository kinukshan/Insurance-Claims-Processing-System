import http from 'k6/http';
import { check, sleep } from 'k6';

const p95Threshold = __ENV.STRICT_SLA === '1' ? 'p(95)<1' : 'p(95)<2000';

export const options = {
    vus: 10,
    duration: '30s',
    thresholds: {
        http_req_duration: [p95Threshold], // Configurable SLA threshold (p95 < 2000ms default; p95 < 1ms for failure demo)
        http_req_failed: ['rate<0.05'],    // Less than 5% errors
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export default function () {
    const res = http.get(`${BASE_URL}/api/policytypes`, {
        headers: {
            'Accept': 'application/json',
        },
    });

    check(res, {
        'status is 200': (r) => r.status === 200,
        'response is json': (r) => r.headers['Content-Type'] && r.headers['Content-Type'].includes('application/json'),
        'has policy types array': (r) => {
            try {
                const body = JSON.parse(r.body);
                return Array.isArray(body) && body.length > 0;
            } catch (e) {
                return false;
            }
        },
    });

    sleep(1);
}

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '10s', target: 5 },  // Ramp up to 5 users
        { duration: '20s', target: 15 }, // Ramp up to 15 users
        { duration: '10s', target: 0 },  // Ramp down to 0 users
    ],
    thresholds: {
        http_req_duration: ['p(95)<1500'],
        http_req_failed: ['rate<0.02'],
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';
const JWT_TOKEN = __ENV.JWT_TOKEN || '';

export default function () {
    const params = {
        headers: {
            'Accept': 'application/json',
            ...(JWT_TOKEN ? { 'Authorization': `Bearer ${JWT_TOKEN}` } : {}),
        },
    };

    const res = http.get(`${BASE_URL}/api/policytypes`, params);

    check(res, {
        'status is 200': (r) => r.status === 200,
        'response time < 1500ms': (r) => r.timings.duration < 1500,
    });

    sleep(0.5);
}

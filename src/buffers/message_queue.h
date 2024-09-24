#ifndef OWLTREE_QUEUE_H
#define OWLTREE_QUEUE_H

#include "../ids/client_id.h"
#include "../ids/network_id.h"
#include "../rpcs/rpc_id.h"
#include <queue>
#include <mutex>

namespace owltree {

struct message {
    public:
        client_id caller;
        client_id callee;
        rpc_id rpc;
        network_id target;
        void* args;
};

class message_queue {
    private:
        std::mutex _lock;
        std::queue<message*> _queue;
    
    public:
        message_queue() {
            _queue = std::queue<message*>();
        }

        ~message_queue() {
            if (!is_empty()) {
                message* cur = nullptr;
                while (try_dequeue(&cur)) {
                    delete cur;
                }
            }
        }

        void enqueue(message* message) {
            _lock.lock();
            _queue.push(message);
            _lock.unlock();
        }

        bool is_empty() {
            _lock.lock();
            bool result = _queue.empty();
            _lock.unlock();
            return result;
        }

        int size() { 
            _lock.lock();
            int size = _queue.size(); 
            _lock.unlock();
            return size;
        }

        bool try_dequeue(message** message) {
            _lock.lock();
            if (_queue.size() == 0) {
                _lock.unlock();
                return false;
            }

            *message = _queue.front();
            _queue.pop();
            _lock.unlock();
            return true;
        }
};

}

#endif
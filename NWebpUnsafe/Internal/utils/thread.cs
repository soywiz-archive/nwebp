using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	// State of the worker thread object
	enum WebPWorkerStatus
	{
		NOT_OK = 0,   // object is unusable
		OK,           // ready to work
		WORK          // busy finishing the current task
	}

	// Function to be called by the worker thread. Takes two opaque pointers as
	// arguments (data1 and data2), and should return false in case of error.
	unsafe delegate int WebPWorkerHook(void*a0, void*a1);

	// Synchronize object used to launch job in the worker thread
	unsafe partial class WebPWorker
	{
		WebPWorkerStatus status_;
		WebPWorkerHook hook;    // hook to call
		void* data1;            // first argument passed to 'hook'
		void* data2;            // second argument passed to 'hook'
		int had_error;          // return value of the last call to 'hook'
	}

	/*



	// Must be called first, before any other method.
	void WebPWorkerInit(WebPWorker* worker);
	// Must be called initialize the object and spawn the thread. Re-entrant.
	// Will potentially launch the thread. Returns false in case of error.
	int WebPWorkerReset(WebPWorker* worker);
	// Make sure the previous work is finished. Returns true if worker.had_error
	// was not set and not error condition was triggered by the working thread.
	int WebPWorkerSync(WebPWorker* worker);
	// Trigger the thread to call hook() with data1 and data2 argument. These
	// hook/data1/data2 can be changed at any time before calling this function,
	// but not be changed afterward until the next call to WebPWorkerSync().
	void WebPWorkerLaunch(WebPWorker* worker);
	// Kill the thread and terminate the object. To use the object again, one
	// must call WebPWorkerReset() again.
	void WebPWorkerEnd(WebPWorker* worker);
	//------------------------------------------------------------------------------


	// _beginthreadex requires __stdcall
	#define THREAD_RETURN(val) (unsigned int)((DWORD_PTR)val)

	static int pthread_create(pthread_t* thread, void* attr, uint (__stdcall *start)(void*), void* arg)
	{
		(void)attr;
		*thread = (pthread_t)_beginthreadex(null,   // void *security 
											0,      // unsigned stack_size 
											start,
											arg,
											0,      // unsigned initflag 
											null);  // unsigned *thrdaddr 
		if (*thread == null) return 1;
		SetThreadPriority(*thread, THREAD_PRIORITY_ABOVE_NORMAL);
		return 0;
	}

	static int pthread_join(pthread_t thread, void** value_ptr) {
		(void)value_ptr;
		return (WaitForSingleObject(thread, INFINITE) != WAIT_OBJECT_0 || CloseHandle(thread) == 0);
	}

	// Mutex
	static int pthread_mutex_init(pthread_mutex_t* mutex, void* mutexattr) {
		(void)mutexattr;
		InitializeCriticalSection(mutex);
		return 0;
	}

	static int pthread_mutex_lock(pthread_mutex_t* mutex) {
		EnterCriticalSection(mutex);
		return 0;
	}

	static int pthread_mutex_unlock(pthread_mutex_t* mutex) {
		LeaveCriticalSection(mutex);
		return 0;
	}

	static int pthread_mutex_destroy(pthread_mutex_t* mutex) {
		DeleteCriticalSection(mutex);
		return 0;
	}

	// Condition
	static int pthread_cond_destroy(pthread_cond_t* condition) {
		int ok = 1;
		ok &= (CloseHandle(condition.waiting_sem_) != 0);
		ok &= (CloseHandle(condition.received_sem_) != 0);
		ok &= (CloseHandle(condition.signal_event_) != 0);
		return !ok;
	}

	static int pthread_cond_init(pthread_cond_t* condition, void* cond_attr) {
		(void)cond_attr;
		condition.waiting_sem_ = CreateSemaphore(null, 0, 1, null);
		condition.received_sem_ = CreateSemaphore(null, 0, 1, null);
		condition.signal_event_ = CreateEvent(null, FALSE, FALSE, null);
		if (condition.waiting_sem_ == null ||
			condition.received_sem_ == null ||
			condition.signal_event_ == null) {
		pthread_cond_destroy(condition);
		return 1;
		}
		return 0;
	}

	static int pthread_cond_signal(pthread_cond_t* condition) {
		int ok = 1;
		if (WaitForSingleObject(condition.waiting_sem_, 0) == WAIT_OBJECT_0) {
		// a thread is waiting in pthread_cond_wait: allow it to be notified
		ok = SetEvent(condition.signal_event_);
		// wait until the event is consumed so the signaler cannot consume
		// the event via its own pthread_cond_wait.
		ok &= (WaitForSingleObject(condition.received_sem_, INFINITE) !=
				WAIT_OBJECT_0);
		}
		return !ok;
	}

	static int pthread_cond_wait(pthread_cond_t* condition,
									pthread_mutex_t* mutex) {
		int ok;
		// note that there is a consumer available so the signal isn't dropped in
		// pthread_cond_signal
		if (!ReleaseSemaphore(condition.waiting_sem_, 1, null))
		return 1;
		// now unlock the mutex so pthread_cond_signal may be issued
		pthread_mutex_unlock(mutex);
		ok = (WaitForSingleObject(condition.signal_event_, INFINITE) ==
			WAIT_OBJECT_0);
		ok &= ReleaseSemaphore(condition.received_sem_, 1, null);
		pthread_mutex_lock(mutex);
		return !ok;
	}

	#else  // _WIN32
	# define THREADFN void*
	# define THREAD_RETURN(val) val
	#endif

	//------------------------------------------------------------------------------

	static THREADFN WebPWorkerThreadLoop(void *ptr) {    // thread loop
		WebPWorker* worker = (WebPWorker*)ptr;
		int done = 0;
		while (!done) {
		pthread_mutex_lock(&worker.mutex_);
		while (worker.status_ == OK) {   // wait in idling mode
			pthread_cond_wait(&worker.condition_, &worker.mutex_);
		}
		if (worker.status_ == WORK) {
			if (worker.hook) {
			worker.had_error |= !worker.hook(worker.data1, worker.data2);
			}
			worker.status_ = OK;
		} else if (worker.status_ == NOT_OK) {   // finish the worker
			done = 1;
		}
		// signal to the main thread that we're done (for Sync())
		pthread_cond_signal(&worker.condition_);
		pthread_mutex_unlock(&worker.mutex_);
		}
		return THREAD_RETURN(null);    // Thread is finished
	}

	// main thread state control
	static void WebPWorkerChangeState(WebPWorker* worker,
										WebPWorkerStatus new_status) {
		// no-op when attempting to change state on a thread that didn't come up
		if (worker.status_ < OK) return;

		pthread_mutex_lock(&worker.mutex_);
		// wait for the worker to finish
		while (worker.status_ != OK) {
		pthread_cond_wait(&worker.condition_, &worker.mutex_);
		}
		// assign new status and release the working thread if needed
		if (new_status != OK) {
		worker.status_ = new_status;
		pthread_cond_signal(&worker.condition_);
		}
		pthread_mutex_unlock(&worker.mutex_);
	}

	#endif

	//------------------------------------------------------------------------------

	void WebPWorkerInit(WebPWorker* worker) {
		memset(worker, 0, sizeof(*worker));
		worker.status_ = NOT_OK;
	}

	int WebPWorkerSync(WebPWorker* worker) {
	#if WEBP_USE_THREAD
		WebPWorkerChangeState(worker, OK);
	#endif
		assert(worker.status_ <= OK);
		return !worker.had_error;
	}

	int WebPWorkerReset(WebPWorker* worker) {
		int ok = 1;
		worker.had_error = 0;
		if (worker.status_ < OK) {
	#if WEBP_USE_THREAD
		if (pthread_mutex_init(&worker.mutex_, null) ||
			pthread_cond_init(&worker.condition_, null)) {
			return 0;
		}
		pthread_mutex_lock(&worker.mutex_);
		ok = !pthread_create(&worker.thread_, null, WebPWorkerThreadLoop, worker);
		if (ok) worker.status_ = OK;
		pthread_mutex_unlock(&worker.mutex_);
	#else
		worker.status_ = OK;
	#endif
		} else if (worker.status_ > OK) {
		ok = WebPWorkerSync(worker);
		}
		assert(!ok || (worker.status_ == OK));
		return ok;
	}

	void WebPWorkerLaunch(WebPWorker* worker) {
	#if WEBP_USE_THREAD
		WebPWorkerChangeState(worker, WORK);
	#else
		if (worker.hook)
		worker.had_error |= !worker.hook(worker.data1, worker.data2);
	#endif
	}

	void WebPWorkerEnd(WebPWorker* worker) {
		if (worker.status_ >= OK) {
	#if WEBP_USE_THREAD
		WebPWorkerChangeState(worker, NOT_OK);
		pthread_join(worker.thread_, null);
		pthread_mutex_destroy(&worker.mutex_);
		pthread_cond_destroy(&worker.condition_);
	#else
		worker.status_ = NOT_OK;
	#endif
		}
		assert(worker.status_ == NOT_OK);
	}

	//------------------------------------------------------------------------------
	*/
}
